# Glowing Jellyfish
Version 2.0 of the Glowing Jellyfish. Good at beating up humans (~2600 on [lichess](https://lichess.org/@/glowingjellyfish/playing)), but still has a very long way to go against its fellow machines (Stockfish crushes it even with rook-odds!)


Note: this is the UCI version of the program, which does not have a graphical interface. The UCI implementation is also very barebones -- I just did the minimum to get it up and running on lichess.

## Build

### Prerequisites
- [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

### Build from the repository root
```bash
dotnet restore Glowing-Jellyfish.sln
dotnet build Glowing-Jellyfish.sln -c Release
```

## Run

Run the UCI engine from the repository root:

```bash
dotnet run --project Glowing-Jellyfish/Glowing-Jellyfish.csproj -c Release
```

The engine reads UCI commands from standard input. For a quick smoke test, send `quit`:

```bash
printf 'quit\n' | dotnet run --project Glowing-Jellyfish/Glowing-Jellyfish.csproj -c Release
```

You can use it directly in a UCI GUI like Arena, Cute Chess, or Banksia by pointing the GUI to the built executable.

## Training / Improving the engine

This project is a classic handcrafted chess engine, not a neural-network model. The `train` command here generates self-play data; it does not perform neural-network optimization.

To improve strength, treat "training" as **tuning and benchmarking**:

1. **Build and run the engine** through a UCI GUI (Arena, Cute Chess, Banksia, etc.).
2. **Change evaluation/search code** in:
   - `src/Core/Evaluation/*`
   - `src/Core/Search/*`
   - `src/Core/Move Generation/*`
3. **Play test matches** (self-play or versus baseline versions) using identical time controls.
4. **Keep changes that gain Elo** and revert ones that do not.

Typical things to tune:
- Piece-square tables (`PieceSquareTable.cs`)
- Evaluation weights (`Evaluation.cs`)
- Move ordering and pruning in search (`MoveOrdering.cs`, `Searcher.cs`)
- Opening book contents (`resources/Book.txt`)

In short: you do not train with datasets; you improve strength by iterative code changes + match testing.

You can now also generate basic self-play training data directly from the app:

- Run the engine
- Type `train` (or `train <games> <maxPly>`, for example `train 25 160`)
- The engine will create a `training-data.csv` file in its app-data directory containing `fen,result` samples (`1` white win, `0` draw, `-1` black win).
