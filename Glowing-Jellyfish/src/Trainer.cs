using Chess.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GlowingJellyfish;

public readonly struct TrainingSummary
{
	public TrainingSummary(int sampleCount, int whiteWins, int blackWins, int draws, string outputPath)
	{
		SampleCount = sampleCount;
		WhiteWins = whiteWins;
		BlackWins = blackWins;
		Draws = draws;
		OutputPath = outputPath;
	}

	public int SampleCount { get; }
	public int WhiteWins { get; }
	public int BlackWins { get; }
	public int Draws { get; }
	public string OutputPath { get; }
}

public class Trainer
{
	readonly Random random = new();

	public TrainingSummary GenerateSelfPlayData(int gameCount, int maxPly, string outputDirectory)
	{
		Directory.CreateDirectory(outputDirectory);
		string outputPath = Path.Combine(outputDirectory, "training-data.csv");
		List<string> rows = new() { "fen,result" };

		int whiteWins = 0;
		int blackWins = 0;
		int draws = 0;

		for (int gameIndex = 0; gameIndex < gameCount; gameIndex++)
		{
			Board board = Board.CreateBoard();
			List<string> fens = new();

			for (int ply = 0; ply < maxPly; ply++)
			{
				GameResult currentState = Arbiter.GetGameState(board);
				if (currentState != GameResult.InProgress)
				{
					break;
				}

				fens.Add(FenUtility.CurrentFen(board));
				Move move = ChooseMove(board);
				board.MakeMove(move);
			}

			GameResult result = Arbiter.GetGameState(board);
			double label = GetLabel(result);

			if (label > 0)
			{
				whiteWins++;
			}
			else if (label < 0)
			{
				blackWins++;
			}
			else
			{
				draws++;
			}

			foreach (string fen in fens)
			{
				rows.Add($"\"{fen}\",{label.ToString(CultureInfo.InvariantCulture)}");
			}
		}

		File.WriteAllLines(outputPath, rows);
		return new TrainingSummary(rows.Count - 1, whiteWins, blackWins, draws, outputPath);
	}

	Move ChooseMove(Board board)
	{
		MoveGenerator moveGenerator = new();
		Span<Move> moves = stackalloc Move[256];
		moveGenerator.GenerateMoves(board, ref moves, capturesOnly: false);

		if (moves.Length == 0)
		{
			return Move.NullMove;
		}

		List<Move> captures = new();
		for (int i = 0; i < moves.Length; i++)
		{
			Move move = moves[i];
			if (!move.IsNull && Piece.PieceType(board.Square[move.TargetSquare]) != Piece.None)
			{
				captures.Add(move);
			}
		}

		if (captures.Count > 0 && random.NextDouble() < 0.6)
		{
			return captures[random.Next(captures.Count)];
		}

		return moves[random.Next(moves.Length)];
	}

	static double GetLabel(GameResult result)
	{
		if (Arbiter.IsWhiteWinsResult(result))
		{
			return 1;
		}
		if (Arbiter.IsBlackWinsResult(result))
		{
			return -1;
		}
		return 0;
	}
}
