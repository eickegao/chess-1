﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Chess.Common.UnitTests
{
    // TODO - test notes
    //  - All moves
    //    - need to test whether it leaves the king in check - if so not allowed!
    //  - Pawn
    //    - en passant
    //       From: https://en.wikipedia.org/wiki/En_passant
    //          The conditions are:
    //              1. the capturing pawn must be on its fifth rank;
    //              2. the captured pawn must be on an adjacent file and must have just moved two squares in a single move(i.e.a double-step move);
    //              3. the capture can only be made on the move immediately after the opposing pawn makes the double-step move; otherwise the right to capture it en passant is lost.
    //  - Rook
    //    - castling!! (need to track whether pieces have moved in Game. Add tests for this ;-) )
    //       From: https://en.wikipedia.org/wiki/Castling
    //
    //          Castling is permissible if and only if all of the following conditions hold(Schiller 2003:19):
    //              1. The king and the chosen rook are on the player's first rank.[3]
    //              2. Neither the king nor the chosen rook has previously moved.
    //              3. There are no pieces between the king and the chosen rook.
    //              4. The king is not currently in check.
    //              5. The king does not pass through a square that is attacked by an enemy piece.[4]
    //              6. The king does not end up in check. (True of any legal move.)


    public class GameMoves_PawnTests
    {
        [Fact]
        public void StartingPosition_White()
        {
            GivenBoard(
            //a  b  c  d  e  f  g  h
            "Br Bn Bb Bq Bk Bb Bn Br\r\n" + //8
            "Bp Bp Bp Bp Bp Bp Bp Bp\r\n" + //7
            "                       \r\n" + //6
            "                       \r\n" + //5
            "                       \r\n" + //4
            "                       \r\n" + //3
            "Wp Wp Wp Wp Wp Wp Wp Wp\r\n" + //2
            "Wr Wn Wb Wq Wk Wb Wn Wr\r\n"   //1
            );
            WhenSelectedPieceIs("e2");
            ThenMovesAre("e3", "e4");
        }
        [Fact]
        public void StartingPosition_Black()
        {
            GivenBoard(
            //a  b  c  d  e  f  g  h
            "Br Bn Bb Bq Bk Bb Bn Br\r\n" + //8
            "Bp Bp Bp Bp Bp Bp Bp Bp\r\n" + //7
            "                       \r\n" + //6
            "                       \r\n" + //5
            "                       \r\n" + //4
            "                       \r\n" + //3
            "Wp Wp Wp Wp Wp Wp Wp Wp\r\n" + //2
            "Wr Wn Wb Wq Wk Wb Wn Wr\r\n"   //1
            );
            WhenSelectedPieceIs("e7");
            ThenMovesAre("e6", "e5");
        }
        [Fact]
        public void PawnAlreadyMoved()
        {
            GivenBoard(
            //a  b  c  d  e  f  g  h
            "Br Bn Bb Bq Bk Bb Bn Br\r\n" + //8
            "Bp Bp Bp Bp Bp Bp Bp Bp\r\n" + //7
            "                       \r\n" + //6
            "                       \r\n" + //5
            "                       \r\n" + //4
            "            Wp         \r\n" + //3
            "Wp Wp Wp Wp    Wp Wp Wp\r\n" + //2
            "Wr Wn Wb Wq Wk Wb Wn Wr\r\n"   //1
            );
            WhenSelectedPieceIs("e3");
            ThenMovesAre("e4");
        }

        [Fact]
        public void NoMoves()
        {
            GivenBoard(
            //a  b  c  d  e  f  g  h
            "Br Bn Bb Bq Bk Bb Bn Br\r\n" + //8
            "Bp Bp Bp    Bp Bp Bp Bp\r\n" + //7
            "                       \r\n" + //6
            "            Bp         \r\n" + //5
            "            Wp         \r\n" + //4
            "                       \r\n" + //3
            "Wp Wp Wp Wp    Wp Wp Wp\r\n" + //2
            "Wr Wn Wb Wq Wk Wb Wn Wr\r\n"   //1
            );
            WhenSelectedPieceIs("e4");
            ThenMovesAre();
        }

        [Fact]
        public void CaptureOne()
        {
            GivenBoard(
            //a  b  c  d  e  f  g  h
            "Br Bn Bb Bq Bk Bb Bn Br\r\n" + //8
            "Bp Bp Bp    Bp Bp Bp Bp\r\n" + //7
            "                       \r\n" + //6
            "         Bp            \r\n" + //5
            "            Wp         \r\n" + //4
            "                       \r\n" + //3
            "Wp Wp Wp Wp    Wp Wp Wp\r\n" + //2
            "Wr Wn Wb Wq Wk Wb Wn Wr\r\n"   //1
            );
            WhenSelectedPieceIs("e4");
            ThenMovesAre("d5", "e5");           // TODO - is pawn force to take?
        }

        [Fact]
        public void CaptureTwo()
        {
            GivenBoard(
            //a  b  c  d  e  f  g  h
            "Br Bn Bb Bq Bk Bb Bn Br\r\n" + //8
            "Bp Bp Bp    Bp    Bp Bp\r\n" + //7
            "                       \r\n" + //6
            "         Bp    Bp      \r\n" + //5
            "            Wp         \r\n" + //4
            "                       \r\n" + //3
            "Wp Wp Wp Wp    Wp Wp Wp\r\n" + //2
            "Wr Wn Wb Wq Wk Wb Wn Wr\r\n"   //1
            );
            WhenSelectedPieceIs("e4");
            ThenMovesAre("d5", "e5", "f5");     // TODO - is pawn force to take?
        }


        private string _board;
        private void GivenBoard(string board)
        {
            _board = board;
        }
        private SquareReference _selectedSquare;
        private void WhenSelectedPieceIs(string selectedSquare)
        {
            _selectedSquare = selectedSquare;
        }
        private void ThenMovesAre(params string[] moves)
        {
            var expectedMoves = moves
                                    .OrderBy(m => m)
                                    .Select(m => (SquareReference)m)
                                    .ToList();

            var board = Board.Parse(_board);
            var selectedPiece = board[_selectedSquare].Piece;
            if (selectedPiece.Color == Color.Empty)
            {
                throw new ArgumentException("Selected square must contain a piece");
            }
            var game = new Game("testgame", selectedPiece.Color, board);

            var actualMoves = game
                                .GetAvailableMoves(_selectedSquare)
                                .OrderBy(s => s.ToString())
                                .ToList();

            Assert.Equal(expectedMoves, actualMoves);
        }
    }
}