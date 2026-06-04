# Task: Inject IScheduler into time‑controlled services

**Description**
- Refactor `ScheduledJobService` (and any other timer‑based code) to accept an `IScheduler` instead of directly using `PeriodicTimer` or `Task.Delay`.
- The default scheduler for production will be `TaskPoolScheduler.Default`.
- Unit tests will inject a `TestScheduler` to deterministically advance virtual time.

**Acceptance Criteria**
1. `ScheduledJobService` constructor signature includes `IScheduler scheduler` (optional with default value).
2. All timer logic uses `Observable.Interval(TimeSpan, scheduler)` or `scheduler.SchedulePeriodic`.
3. Existing production code passes the default scheduler; test code passes a `TestScheduler`.
4. No observable change in job execution frequency for real users.
5. All existing scheduled‑job tests still pass after adjustment.

**Tests to Create**
- Unit test that creates `ScheduledJobService` with a `TestScheduler`, schedules a job, advances time, and asserts the job ran the expected number of times without real delays.
- Verify that disposing the service disposes the scheduler subscription.
- Add a test confirming that the default constructor (no scheduler argument) uses `TaskPoolScheduler.Default` (could be a simple `Assert.IsType<TaskPoolScheduler>` check).