﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Chess.Web.Models.Game;
using Chess.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.ApplicationInsights;
using Chess.Common;

namespace Chess.Web.Controllers
{
    [Route("")]
    public class GameController : Controller
    {
        private readonly IGameStore _gameStore;
        private readonly IConfiguration _configuration;

        public GameController(IGameStore gameStore, IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _gameStore = gameStore;
            _configuration = configuration;
            _telemetryClient = telemetryClient;
        }

        [HttpGet("", Name = "Home")]
        public IActionResult Home()
        {
            return View();
        }

        [HttpPost("play/new")]
        public IActionResult StartNew()
        {
            var game = Common.Game.CreateStartingGame();
            _gameStore.Save(game);

            _telemetryClient.TrackEvent("StartGame", new Dictionary<string, string>
            {
                { "GameId", game.Id }
            });

            return RedirectToAction(nameof(ChoosePiece), new { gameId = game.Id });
        }

        [HttpGet("play/{gameId}", Name = "ChoosePiece")]
        public IActionResult ChoosePiece(string gameId)
        {
            // TODO - check whether there are any moves!

            var game = _gameStore.GetGame(gameId);

            var model = MapToChoosePieceModel(game);
            if (model.HasMoves)
            {
                return View("ShowGame", model);
            }
            else
            {
                if (model.InCheck)
                {
                    // Checkmate
                    _telemetryClient.TrackEvent("GameEnd", new Dictionary<string, string>
                    {
                        { "GameId", game.Id },
                        { "EndType", "checkmate" },
                        { "WinningColor" , model.Opponent.ToString() },
                    });
                    return View("Checkmate", model);
                }
                else
                {
                    // Stalemate
                    _telemetryClient.TrackEvent("GameEnd", new Dictionary<string, string>
                    {
                        { "GameId", game.Id },
                        { "EndType", "Stalemate" },
                    });
                    return View("Stalemate", model);
                }
            }
        }

        // TODO - enable binding for SquareReference type
        [HttpGet("play/{gameId}/{pieceSquareRef}")]
        public IActionResult ChooseEndPosition(string gameId, string pieceSquareRef) // eg b3
        {
            var game = _gameStore.GetGame(gameId);
            var pieceReference = (Common.SquareReference)pieceSquareRef;

            var model = MapToChooseEndPositionModel(game, pieceReference);
            return View(model);
        }

        [HttpGet("play/{gameId}/{pieceSquareRef}/{endPosition}")]
        public IActionResult Confirm(string gameId, string pieceSquareRef, string endPosition)
        {
            // Called to prompt user to confirm
            var game = _gameStore.GetGame(gameId);
            var pieceReference = (Common.SquareReference)pieceSquareRef;
            var endPositionReference = (Common.SquareReference)endPosition;

            // Move the piece on a copy
            game = new Common.Game(game.Id, game.CurrentTurn, game.Board.MovePiece(pieceReference, endPositionReference), game.Moves.ToList());
            var model = MapToConfirmModel(game, pieceReference, endPositionReference);
            return View(model);
        }

        [HttpPost("play/{gameId}/{pieceSquareRef}/{endPosition}")]
        public IActionResult Confirmed(string gameId, string pieceSquareRef, string endPosition)
        {
            // Called when user has confirmed
            var game = _gameStore.GetGame(gameId);
            var pieceReference = (Common.SquareReference)pieceSquareRef;
            var endPositionReference = (Common.SquareReference)endPosition;

            var movedPiece = game.Board[pieceReference].Piece;
            var capturePiece = game.Board[endPositionReference].Piece;

            // Move the piece and save
            game = game.MakeMove(pieceReference, endPositionReference);
            _gameStore.Save(game);

            _telemetryClient.TrackEvent("MovePiece", new Dictionary<string, string>
            {
                { "GameId", game.Id },
                { "Color" , game.CurrentTurn.ToString() },
                { "Piece", movedPiece.PieceType.ToString() },
                { "Captured" , capturePiece.PieceType == Common.PieceType.Empty ? null : capturePiece.PieceType.ToString() },
                { "PutInCheck", game.CurrentPlayerInCheck.ToString() }
            });

            return RedirectToAction(nameof(ChoosePiece));
        }

        static readonly string[] SquareColors = new[] { "white", "black" };
        private GameModel MapToChoosePieceModel(Common.Game game)
        {
            var hasMoves = false;
            var model = new GameModel
            {
                CurrentPlayer = game.CurrentTurn,
                Opponent = game.CurrentTurn == Common.Color.Black ? Common.Color.White : Common.Color.Black,
                InCheck = game.CurrentPlayerInCheck,
                Board = new Models.Game.Board
                {
                    Squares = game.Board.Squares
                                .Select((row, rowIndex) =>
                                    row.Select((square, columnIndex) =>
                                    {
                                        // highlight current player's pieces with moves
                                        bool canSelect = square.Piece.Color == game.CurrentTurn
                                                            && game.GetAvailableMoves(square.Reference).Any();
                                        if (canSelect)
                                        {
                                            hasMoves = true;
                                        }
                                        string squareRef = square.Reference.ToString();
                                        return new BoardSquare
                                        {
                                            PieceImage = ImageNameFromPiece(square.Piece),
                                            PieceName = square.Piece.Color + " " + square.Piece.PieceType,
                                            SquareColour = SquareColors[(rowIndex + columnIndex) % 2],
                                            CanSelect = canSelect,
                                            SelectUrl = canSelect
                                                            ? Url.Action(nameof(ChooseEndPosition), new { pieceSquareRef = squareRef })
                                                            : null,
                                            ReferenceString = squareRef
                                        };
                                    })
                                    .ToArray()
                                ).ToArray()
                },
                MoveHistory = MapToHistoricalMoves(game.Moves)
            };
            model.HasMoves = hasMoves;
            return model;
        }
        private GameModel MapToChooseEndPositionModel(Common.Game game, Common.SquareReference selectedSquareReference)
        {
            Common.SquareReference[] availableMoves = game.GetAvailableMoves(selectedSquareReference).ToArray();
            return new GameModel
            {
                CurrentPlayer = game.CurrentTurn,
                Opponent = game.CurrentTurn == Common.Color.Black ? Common.Color.White : Common.Color.Black,
                InCheck = game.CurrentPlayerInCheck,
                Board = new Models.Game.Board
                {
                    Squares = game.Board.Squares
                                .Select((row, rowIndex) =>
                                    row.Select((square, columnIndex) =>
                                    {
                                        bool canSelect = availableMoves.Contains(square.Reference);
                                        string squareRef = square.Reference.ToString();
                                        string selectUrl = canSelect
                                                            ? Url.Action(nameof(Confirm), new { pieceSquareRef = selectedSquareReference.ToString(), endPosition = squareRef })
                                                            : null;
                                        return new BoardSquare
                                        {
                                            PieceImage = ImageNameFromPiece(square.Piece),
                                            PieceName = square.Piece.Color + " " + square.Piece.PieceType,
                                            SquareColour = SquareColors[(rowIndex + columnIndex) % 2],
                                            CanSelect = canSelect,
                                            Reference = square.Reference,
                                            SelectUrl = selectUrl,
                                            ReferenceString = squareRef
                                        };
                                    })
                                    .ToArray()
                                ).ToArray(),
                    SelectedSquare = selectedSquareReference
                },
                MoveHistory = MapToHistoricalMoves(game.Moves)
            };
        }
        private GameModel MapToConfirmModel(
            Common.Game game,
            Common.SquareReference selectedSquareReference,
            Common.SquareReference endPosition)
        {
            //Common.SquareReference[] availableMoves = game.GetAvailableMoves();
            return new GameModel
            {
                CurrentPlayer = game.CurrentTurn,
                Opponent = game.CurrentTurn == Common.Color.Black ? Common.Color.White : Common.Color.Black,
                Board = new Models.Game.Board
                {
                    Squares = game.Board.Squares
                                .Select((row, rowIndex) =>
                                    row.Select((square, columnIndex) =>
                                    {
                                        // highlight any space or opponent piece
                                        // TODO add proper available move calculation!!
                                        bool canSelect = square.Piece.Color != game.CurrentTurn;
                                        string squareRef = square.Reference.ToString();
                                        return new BoardSquare
                                        {
                                            PieceImage = ImageNameFromPiece(square.Piece),
                                            PieceName = square.Piece.Color + " " + square.Piece.PieceType,
                                            SquareColour = SquareColors[(rowIndex + columnIndex) % 2],
                                            CanSelect = false,
                                            Reference = square.Reference,
                                            ReferenceString = squareRef
                                        };
                                    })
                                    .ToArray()
                                ).ToArray(),
                    SelectedSquare = selectedSquareReference
                },
                MoveHistory = MapToHistoricalMoves(game.Moves)
            };
        }

        private List<HistoricalMove> MapToHistoricalMoves(IList<Move> moves)
        {
            string MoveToString(Move move)
            {
                if (move == null)
                {
                    return "";
                }
                string captured = null;
                if (move.CapturedPiece.PieceType != PieceType.Empty)
                {
                    captured = "x" + NotationPieceTypes[move.CapturedPiece.PieceType];
                }
                return $"{NotationPieceTypes[move.Piece.PieceType]}{move.Start}-{move.End}{captured}";
            }
            return moves.Pair()
                        .Select((pair, index) => new HistoricalMove
                        {
                            // TODO
                            // - check move format
                            // - indicate piece capture, check?
                            TurnNumber = index + 1,
                            White = MoveToString(pair.Item1),
                            Black = MoveToString(pair.Item2)
                        })
                        .ToList();
        }


        static readonly Dictionary<Common.Color, char> ImageColors = new Dictionary<Common.Color, char>
        {
            {Color.Black, 'd' },
            {Color.White, 'l' },
        };
        static readonly Dictionary<Common.PieceType, char> ImagePieceTypes = new Dictionary<Common.PieceType, char>
        {
            {PieceType.Pawn, 'p' },
            {PieceType.Rook, 'r' },
            {PieceType.Knight, 'n' },
            {PieceType.Bishop, 'b' },
            {PieceType.Queen, 'q' },
            {PieceType.King, 'k' },
        };
        static readonly Dictionary<PieceType, string> NotationPieceTypes = new Dictionary<PieceType, string>
        {
            {PieceType.Pawn, "P" },
            {PieceType.Rook, "R" },
            {PieceType.Knight, "Kt" },
            {PieceType.Bishop, "B" },
            {PieceType.Queen, "Q" },
            {PieceType.King, "K" },
        };
        private TelemetryClient _telemetryClient;

        private string ImageNameFromPiece(Common.Piece piece)
        {
            if (piece.PieceType == Common.PieceType.Empty)
            {
                return null;
            }
            char lightDark = ImageColors[piece.Color];
            char pieceType = ImagePieceTypes[piece.PieceType];
            return $"200px-Chess_{pieceType}{lightDark}t45.svg.png";
        }
    }
}
