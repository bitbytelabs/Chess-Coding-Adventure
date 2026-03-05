using System;
using System.Globalization;
using System.IO;

namespace Chess.Core
{
	public readonly struct PieceValues
	{
		public PieceValues(int pawnValue, int knightValue, int bishopValue, int rookValue, int queenValue)
		{
			PawnValue = pawnValue;
			KnightValue = knightValue;
			BishopValue = bishopValue;
			RookValue = rookValue;
			QueenValue = queenValue;
		}

		public int PawnValue { get; }
		public int KnightValue { get; }
		public int BishopValue { get; }
		public int RookValue { get; }
		public int QueenValue { get; }
	}

	public static class EvaluationTuning
	{
		public static PieceValues Default => new(100, 300, 320, 500, 900);
		public static PieceValues Current { get; private set; } = Default;

		public static void Apply(PieceValues values)
		{
			Current = Sanitize(values);
		}

		public static void Load(string path)
		{
			if (!File.Exists(path))
			{
				Current = Default;
				return;
			}

			string[] lines = File.ReadAllLines(path);
			int pawn = Current.PawnValue;
			int knight = Current.KnightValue;
			int bishop = Current.BishopValue;
			int rook = Current.RookValue;
			int queen = Current.QueenValue;

			foreach (string line in lines)
			{
				string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
				if (parts.Length != 2)
				{
					continue;
				}

				if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
				{
					continue;
				}

				switch (parts[0].ToLowerInvariant())
				{
					case "pawn":
						pawn = value;
						break;
					case "knight":
						knight = value;
						break;
					case "bishop":
						bishop = value;
						break;
					case "rook":
						rook = value;
						break;
					case "queen":
						queen = value;
						break;
				}
			}

			Current = Sanitize(new PieceValues(pawn, knight, bishop, rook, queen));
		}

		public static void Save(string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
			string[] lines =
			{
				$"pawn={Current.PawnValue.ToString(CultureInfo.InvariantCulture)}",
				$"knight={Current.KnightValue.ToString(CultureInfo.InvariantCulture)}",
				$"bishop={Current.BishopValue.ToString(CultureInfo.InvariantCulture)}",
				$"rook={Current.RookValue.ToString(CultureInfo.InvariantCulture)}",
				$"queen={Current.QueenValue.ToString(CultureInfo.InvariantCulture)}"
			};
			File.WriteAllLines(path, lines);
		}

		static PieceValues Sanitize(PieceValues values)
		{
			int pawn = Math.Clamp(values.PawnValue, 50, 200);
			int knight = Math.Clamp(values.KnightValue, 150, 600);
			int bishop = Math.Clamp(values.BishopValue, 150, 650);
			int rook = Math.Clamp(values.RookValue, 250, 1100);
			int queen = Math.Clamp(values.QueenValue, 400, 1800);

			knight = Math.Max(knight, pawn + 80);
			bishop = Math.Max(bishop, knight);
			rook = Math.Max(rook, bishop + 120);
			queen = Math.Max(queen, rook + 180);

			return new PieceValues(pawn, knight, bishop, rook, queen);
		}
	}
}
