```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i5-6600K CPU 3.50GHz (Skylake), 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  Job-EGSULU : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=10  RunStrategy=Throughput
UnrollFactor=1  WarmupCount=3

```
| Method                                | QueueDepth | Mean         | Error      | StdDev     | Allocated  |
|-------------------------------------- |----------- |-------------:|-----------:|-----------:|-----------:|
| **PublishInterestSet_EmptyQueue**         | **10**         |     **7.990 μs** |   **1.646 μs** |   **1.089 μs** |    **1.26 KB** |
| PublishInterestSet_AllVisible         | 10         |    54.589 μs |  10.938 μs |   6.509 μs |   25.91 KB |
| PublishInterestSet_NoneVisible        | 10         |   102.300 μs |  34.649 μs |  20.619 μs |    50.8 KB |
| PublishInterestSet_MixedVisibility    | 10         |   144.110 μs |  28.433 μs |  18.807 μs |    77.6 KB |
| DrainQueue_FifoFallback               | 10         |    80.700 μs |  23.492 μs |  13.979 μs |   37.98 KB |
| DrainQueue_VisiblePromoted            | 10         |    50.122 μs |  16.158 μs |   9.615 μs |   16.55 KB |
| FastScrollStress_ThreeCycles          | 10         |   238.850 μs |  16.908 μs |   8.843 μs |  124.55 KB |
| DrainQueue_PriorityDistanceOrdered    | 10         | 2,681.012 μs | 485.934 μs | 254.153 μs | 1251.66 KB |
| FastScrollStress_PriorityCenterChange | 10         | 6,028.837 μs | 791.267 μs | 413.848 μs | 2377.65 KB |
| **PublishInterestSet_EmptyQueue**         | **50**         |     **7.330 μs** |   **2.215 μs** |   **1.465 μs** |    **1.26 KB** |
| PublishInterestSet_AllVisible         | 50         |    53.580 μs |  10.825 μs |   7.160 μs |   26.71 KB |
| PublishInterestSet_NoneVisible        | 50         |   114.206 μs |  34.654 μs |  20.622 μs |   50.66 KB |
| PublishInterestSet_MixedVisibility    | 50         |   131.583 μs |  27.433 μs |  16.325 μs |    77.6 KB |
| DrainQueue_FifoFallback               | 50         |    84.322 μs |  16.935 μs |  10.078 μs |   38.63 KB |
| DrainQueue_VisiblePromoted            | 50         |   148.489 μs |  27.915 μs |  16.612 μs |   72.54 KB |
| FastScrollStress_ThreeCycles          | 50         |   277.111 μs |  74.197 μs |  44.153 μs |  126.27 KB |
| DrainQueue_PriorityDistanceOrdered    | 50         | 2,641.325 μs | 206.633 μs | 108.073 μs |  1235.2 KB |
| FastScrollStress_PriorityCenterChange | 50         | 6,146.770 μs | 768.293 μs | 508.178 μs | 2316.02 KB |
