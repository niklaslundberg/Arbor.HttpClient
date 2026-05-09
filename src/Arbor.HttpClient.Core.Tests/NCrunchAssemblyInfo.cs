#if NCRUNCH
// NCrunch runs tests one-by-one in isolated grains and cannot reliably plan execution
// when xunit.v3 tests run in parallel inside the same assembly. Disabling parallelization
// only under NCrunch keeps regular CLI / VS Test Explorer / CI runs fully parallel
// while preventing the "test was not executed during a planned execution run" warning.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
#endif
