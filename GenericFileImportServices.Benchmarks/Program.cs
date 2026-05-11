using BenchmarkDotNet.Running;

// Run with: dotnet run -c Release [-- --filter *]
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
