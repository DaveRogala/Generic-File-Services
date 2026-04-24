```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), Arm64 RyuJIT armv8.0-a


```
| Method            | N     | Mean              | Error             | StdDev            | Gen0   | Allocated |
|------------------ |------ |------------------:|------------------:|------------------:|-------:|----------:|
| **GetAddEntities**    | **100**   |      **9,585.433 ns** |        **34.4463 ns** |        **32.2211 ns** | **0.7477** |    **6296 B** |
| GetUpdateEntities | 100   |          2.983 ns |         0.0227 ns |         0.0212 ns | 0.0038 |      32 B |
| GetDeleteEntities | 100   |      7,023.804 ns |        34.0815 ns |        30.2124 ns | 0.3586 |    3016 B |
| **GetAddEntities**    | **1000**  |    **726,383.809 ns** |     **1,498.8887 ns** |     **1,170.2337 ns** | **6.8359** |   **60296 B** |
| GetUpdateEntities | 1000  |          3.022 ns |         0.0470 ns |         0.0417 ns | 0.0038 |      32 B |
| GetDeleteEntities | 1000  |    598,438.406 ns |     3,357.8037 ns |     2,803.9181 ns | 2.9297 |   28216 B |
| **GetAddEntities**    | **10000** | **93,503,023.298 ns** | **1,383,078.5743 ns** | **1,226,063.1315 ns** |      **-** |  **600296 B** |
| GetUpdateEntities | 10000 |          2.991 ns |         0.0412 ns |         0.0385 ns | 0.0038 |      32 B |
| GetDeleteEntities | 10000 | 63,401,218.393 ns |   529,023.3802 ns |   468,965.4473 ns |      - |  280216 B |
