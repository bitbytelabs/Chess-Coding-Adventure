using Chess.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GlowingJellyfish;

public readonly struct TrainingSummary
{
	public TrainingSummary(int sampleCount, int whiteWins, int blackWins, int draws, string outputPath, string weightsPath, PieceValues trainedValues)
	{
		SampleCount = sampleCount;
		WhiteWins = whiteWins;
		BlackWins = blackWins;
		Draws = draws;
		OutputPath = outputPath;
		WeightsPath = weightsPath;
		TrainedValues = trainedValues;
	}

	public int SampleCount { get; }
	public int WhiteWins { get; }
	public int BlackWins { get; }
	public int Draws { get; }
	public string OutputPath { get; }
	public string WeightsPath { get; }
	public PieceValues TrainedValues { get; }
}

public class Trainer
{
	readonly Random random = new();

	public TrainingSummary GenerateSelfPlayData(int gameCount, int maxPly, string outputDirectory)
	{
		Directory.CreateDirectory(outputDirectory);
		string outputPath = Path.Combine(outputDirectory, "training-data.csv");
		string weightsPath = Path.Combine(outputDirectory, "trained-piece-values.txt");
		List<string> rows = new() { "fen,result" };
		List<TrainingSample> samples = new();

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
				samples.Add(CreateSample(fen, label));
			}
		}

		File.WriteAllLines(outputPath, rows);

		PieceValues tunedValues = LearnPieceValues(samples);
		EvaluationTuning.Apply(tunedValues);
		EvaluationTuning.Save(weightsPath);

		return new TrainingSummary(rows.Count - 1, whiteWins, blackWins, draws, outputPath, weightsPath, tunedValues);
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

		if (random.NextDouble() < 0.25)
		{
			return moves[random.Next(moves.Length)];
		}

		Evaluation evaluation = new();
		Move bestMove = moves[0];
		int bestScore = int.MinValue;

		for (int i = 0; i < moves.Length; i++)
		{
			Move move = moves[i];
			board.MakeMove(move, inSearch: true);
			int score = -evaluation.Evaluate(board);
			board.UnmakeMove(move, inSearch: true);

			if (score > bestScore)
			{
				bestScore = score;
				bestMove = move;
			}
		}

		return bestMove;
	}

	static TrainingSample CreateSample(string fen, double label)
	{
		Board position = Board.CreateBoard();
		position.LoadPosition(fen);

		int pawnDiff = position.Pawns[Board.WhiteIndex].Count - position.Pawns[Board.BlackIndex].Count;
		int knightDiff = position.Knights[Board.WhiteIndex].Count - position.Knights[Board.BlackIndex].Count;
		int bishopDiff = position.Bishops[Board.WhiteIndex].Count - position.Bishops[Board.BlackIndex].Count;
		int rookDiff = position.Rooks[Board.WhiteIndex].Count - position.Rooks[Board.BlackIndex].Count;
		int queenDiff = position.Queens[Board.WhiteIndex].Count - position.Queens[Board.BlackIndex].Count;

		return new TrainingSample(pawnDiff, knightDiff, bishopDiff, rookDiff, queenDiff, label);
	}

	static PieceValues LearnPieceValues(List<TrainingSample> samples)
	{
		if (samples.Count == 0)
		{
			return EvaluationTuning.Default;
		}

		double pawnW = LearnWeight(samples, s => s.PawnDiff);
		double knightW = LearnWeight(samples, s => s.KnightDiff);
		double bishopW = LearnWeight(samples, s => s.BishopDiff);
		double rookW = LearnWeight(samples, s => s.RookDiff);
		double queenW = LearnWeight(samples, s => s.QueenDiff);

		double pawnScale = Math.Abs(pawnW) < 1e-8 ? 1 : 100.0 / Math.Abs(pawnW);

		int pawn = (int)Math.Round(Math.Abs(pawnW) * pawnScale);
		int knight = (int)Math.Round(Math.Abs(knightW) * pawnScale);
		int bishop = (int)Math.Round(Math.Abs(bishopW) * pawnScale);
		int rook = (int)Math.Round(Math.Abs(rookW) * pawnScale);
		int queen = (int)Math.Round(Math.Abs(queenW) * pawnScale);

		if (knight == 0 || bishop == 0 || rook == 0 || queen == 0)
		{
			return EvaluationTuning.Default;
		}

		return new PieceValues(pawn, knight, bishop, rook, queen);
	}

	static double LearnWeight(List<TrainingSample> samples, Func<TrainingSample, int> selector)
	{
		double numerator = 0;
		double denominator = 0;

		foreach (TrainingSample sample in samples)
		{
			int feature = selector(sample);
			numerator += feature * sample.Label;
			denominator += feature * feature;
		}

		if (Math.Abs(denominator) < 1e-8)
		{
			return 0;
		}

		return numerator / denominator;
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

	readonly struct TrainingSample
	{
		public TrainingSample(int pawnDiff, int knightDiff, int bishopDiff, int rookDiff, int queenDiff, double label)
		{
			PawnDiff = pawnDiff;
			KnightDiff = knightDiff;
			BishopDiff = bishopDiff;
			RookDiff = rookDiff;
			QueenDiff = queenDiff;
			Label = label;
		}

		public int PawnDiff { get; }
		public int KnightDiff { get; }
		public int BishopDiff { get; }
		public int RookDiff { get; }
		public int QueenDiff { get; }
		public double Label { get; }
	}
}
