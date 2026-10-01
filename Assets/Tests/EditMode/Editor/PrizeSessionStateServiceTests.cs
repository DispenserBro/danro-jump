using DanroJump.Prizes;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class PrizeSessionStateServiceTests
    {
        [Test]
        public void TryBeginSession_CreatesActivePendingRecord()
        {
            var service = new PrizeSessionStateService();
            var result = new PrizeFlowResult(PrizeRewardKind.QRCode, 120, 4, 55f, "run-1");

            var started = service.TryBeginSession(result, out var record);

            Assert.That(started, Is.True);
            Assert.That(record, Is.Not.Null);
            Assert.That(record.SessionId, Is.EqualTo("run-1"));
            Assert.That(record.Status, Is.EqualTo(PrizeSessionStatus.Pending));
            Assert.That(service.ActiveSession, Is.SameAs(record));
            Assert.That(service.History.Count, Is.EqualTo(1));
            Assert.That(service.StartedCount, Is.EqualTo(1));
        }

        [Test]
        public void TryBeginSession_BlocksDuplicateSessionId()
        {
            var service = new PrizeSessionStateService();
            var result = new PrizeFlowResult(PrizeRewardKind.PrizeStand, 200, 2, 80f, "same-run");

            Assert.That(service.TryBeginSession(result, out _), Is.True);
            Assert.That(service.TryBeginSession(result, out var duplicate), Is.False);

            Assert.That(duplicate.Status, Is.EqualTo(PrizeSessionStatus.DuplicateBlocked));
            Assert.That(service.DuplicateBlockedCount, Is.EqualTo(1));
            Assert.That(service.StartedCount, Is.EqualTo(1));
            Assert.That(service.History.Count, Is.EqualTo(2));
        }

        [Test]
        public void TryBeginSession_BlocksNewPrizeWhileAnotherSessionIsActive()
        {
            var service = new PrizeSessionStateService();
            service.TryBeginSession(new PrizeFlowResult(PrizeRewardKind.QRCode, 120, 0, 40f, "active-run"), out var active);

            var started = service.TryBeginSession(
                new PrizeFlowResult(PrizeRewardKind.Big, 300, 1, 100f, "next-run"),
                out var blocked);

            Assert.That(started, Is.False);
            Assert.That(service.ActiveSession, Is.SameAs(active));
            Assert.That(blocked.Status, Is.EqualTo(PrizeSessionStatus.DuplicateBlocked));
            Assert.That(blocked.Note, Does.Contain("ещё активна"));
            Assert.That(service.DuplicateBlockedCount, Is.EqualTo(1));
        }

        [Test]
        public void CompleteActiveSession_ClearsActiveSessionAndUpdatesStatistics()
        {
            var service = new PrizeSessionStateService();
            service.TryBeginSession(new PrizeFlowResult(PrizeRewardKind.Big, 300, 0, 120f, "run-2"), out _);
            service.MarkActiveSessionShown();

            var completed = service.CompleteActiveSession("closed");

            Assert.That(completed, Is.True);
            Assert.That(service.ActiveSession, Is.Null);
            Assert.That(service.CompletedCount, Is.EqualTo(1));
            Assert.That(service.History[0].Status, Is.EqualTo(PrizeSessionStatus.Completed));
            Assert.That(service.History[0].Note, Is.EqualTo("closed"));
        }

        [Test]
        public void TryBeginSession_BlocksSameSessionAfterCompletion()
        {
            var service = new PrizeSessionStateService();
            var result = new PrizeFlowResult(PrizeRewardKind.Hopper, 400, 0, 130f, "completed-run");
            service.TryBeginSession(result, out _);
            service.CompleteActiveSession("done");

            var restarted = service.TryBeginSession(result, out var blocked);

            Assert.That(restarted, Is.False);
            Assert.That(blocked.Status, Is.EqualTo(PrizeSessionStatus.DuplicateBlocked));
            Assert.That(service.ActiveSession, Is.Null);
            Assert.That(service.DuplicateBlockedCount, Is.EqualTo(1));
        }

        [Test]
        public void FailActiveSession_ClearsActiveSessionAndUpdatesStatistics()
        {
            var service = new PrizeSessionStateService();
            service.TryBeginSession(new PrizeFlowResult(PrizeRewardKind.QRCode, 150, 1, 60f, "run-3"), out _);

            var failed = service.FailActiveSession("no phone");

            Assert.That(failed, Is.True);
            Assert.That(service.ActiveSession, Is.Null);
            Assert.That(service.FailedCount, Is.EqualTo(1));
            Assert.That(service.History[0].Status, Is.EqualTo(PrizeSessionStatus.Failed));
            Assert.That(service.History[0].Note, Is.EqualTo("no phone"));
        }

        [Test]
        public void BuildSummary_IncludesCountersAndLastRecord()
        {
            var service = new PrizeSessionStateService();
            service.TryBeginSession(new PrizeFlowResult(PrizeRewardKind.Small, 90, 0, 20f, "run-4"), out _);
            service.MarkActiveSessionShown();

            var summary = service.BuildSummary();

            Assert.That(summary, Does.Contain("начато 1"));
            Assert.That(summary, Does.Contain("Активная выдача"));
            Assert.That(summary, Does.Contain("малый приз"));
            Assert.That(summary, Does.Contain("показана"));
        }

        [Test]
        public void Constructor_RestoresCompletedSessionAndStillBlocksDuplicateAfterRestart()
        {
            var repository = new StubPrizeSessionStateRepository(new PrizeSessionStateSnapshot(
                new[]
                {
                    new PrizeSessionRecord(
                        "restored-run",
                        new PrizeFlowResult(PrizeRewardKind.Hopper, 400, 0, 120f, "restored-run"),
                        PrizeSessionStatus.Completed,
                        Utc(10),
                        Utc(11),
                        "done")
                },
                new[] { "restored-run" },
                activeSessionId: string.Empty,
                startedCount: 1,
                completedCount: 1,
                failedCount: 0,
                duplicateBlockedCount: 0,
                fallbackSessionCounter: 0));

            var service = new PrizeSessionStateService(repository);
            var restarted = service.TryBeginSession(
                new PrizeFlowResult(PrizeRewardKind.Hopper, 400, 0, 120f, "restored-run"),
                out var blocked);

            Assert.That(restarted, Is.False);
            Assert.That(blocked, Is.Not.Null);
            Assert.That(blocked.Status, Is.EqualTo(PrizeSessionStatus.DuplicateBlocked));
            Assert.That(service.DuplicateBlockedCount, Is.EqualTo(1));
            Assert.That(service.History.Count, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_RecoversUnfinishedActiveSessionAsFailedAndPersistsUpdatedSnapshot()
        {
            var repository = new StubPrizeSessionStateRepository(new PrizeSessionStateSnapshot(
                new[]
                {
                    new PrizeSessionRecord(
                        "crash-run",
                        new PrizeFlowResult(PrizeRewardKind.QRCode, 150, 1, 60f, "crash-run"),
                        PrizeSessionStatus.Shown,
                        Utc(20),
                        Utc(21),
                        "qr opened")
                },
                new[] { "crash-run" },
                activeSessionId: "crash-run",
                startedCount: 1,
                completedCount: 0,
                failedCount: 0,
                duplicateBlockedCount: 0,
                fallbackSessionCounter: 0));

            var service = new PrizeSessionStateService(repository);

            Assert.That(service.ActiveSession, Is.Null);
            Assert.That(service.FailedCount, Is.EqualTo(1));
            Assert.That(service.History.Count, Is.EqualTo(1));
            Assert.That(service.History[0].Status, Is.EqualTo(PrizeSessionStatus.Failed));
            Assert.That(service.History[0].Note, Does.Contain("восстановлена после перезапуска"));
            Assert.That(repository.SaveCalls, Is.EqualTo(1));
            Assert.That(repository.LastSavedSnapshot, Is.Not.Null);
            Assert.That(repository.LastSavedSnapshot.ActiveSessionId, Is.Empty);
        }

        [Test]
        public void StateChanges_PersistSnapshotOnBeginAndCompletion()
        {
            var repository = new StubPrizeSessionStateRepository();
            var service = new PrizeSessionStateService(repository);

            Assert.That(service.TryBeginSession(
                new PrizeFlowResult(PrizeRewardKind.Big, 320, 0, 90f, "persist-run"),
                out _), Is.True);
            Assert.That(service.CompleteActiveSession("done"), Is.True);

            Assert.That(repository.SaveCalls, Is.GreaterThanOrEqualTo(2));
            Assert.That(repository.LastSavedSnapshot, Is.Not.Null);
            Assert.That(repository.LastSavedSnapshot.CompletedCount, Is.EqualTo(1));
            Assert.That(repository.LastSavedSnapshot.ActiveSessionId, Is.Empty);
        }

        [Test]
        public void StatisticsCounters_IncrementCorrectly()
        {
            var service = new PrizeSessionStateService();
            service.RecordGameplaySession("test-session", 100, 5, 25.5f, 1, PrizeRewardKind.None);
            Assert.That(service.TotalGamesPlayed, Is.EqualTo(1));
            Assert.That(service.TotalScore, Is.EqualTo(100));
            Assert.That(service.TotalCoins, Is.EqualTo(5));

            service.RecordQrShow();
            Assert.That(service.QrShows, Is.EqualTo(1));

            service.RecordComError();
            Assert.That(service.ComErrors, Is.EqualTo(1));

            service.RecordPhysicalDispense(true);
            Assert.That(service.PhysicalDispenses, Is.EqualTo(1));
            Assert.That(service.PhysicalDispenseFailures, Is.EqualTo(0));

            service.RecordPhysicalDispense(false);
            Assert.That(service.PhysicalDispenses, Is.EqualTo(1));
            Assert.That(service.PhysicalDispenseFailures, Is.EqualTo(1));
        }

        private static System.DateTime Utc(int minutes)
        {
            return new System.DateTime(2026, 6, 16, 12, minutes, 0, System.DateTimeKind.Utc);
        }

        private sealed class StubPrizeSessionStateRepository : IPrizeSessionStateRepository
        {
            private readonly PrizeSessionStateSnapshot snapshot;

            public StubPrizeSessionStateRepository(PrizeSessionStateSnapshot snapshot = null)
            {
                this.snapshot = snapshot;
            }

            public int SaveCalls { get; private set; }
            public PrizeSessionStateSnapshot LastSavedSnapshot { get; private set; }

            public bool TryLoad(out PrizeSessionStateSnapshot loadedSnapshot)
            {
                loadedSnapshot = snapshot;
                return loadedSnapshot != null;
            }

            public bool Save(PrizeSessionStateSnapshot newSnapshot)
            {
                SaveCalls++;
                LastSavedSnapshot = newSnapshot;
                return true;
            }
        }
    }
}
