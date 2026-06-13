using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.Results;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Test.Download.DownloadClientTests
{
    /// <summary>
    /// Abstract contract-test base that every <see cref="IDownloadClientV2"/> adapter
    /// must satisfy.  Derive a concrete fixture for each adapter, wire the subject
    /// using mocked dependencies, then the tests here run automatically.
    ///
    /// Purpose:
    ///   - Ensure GetQueue() never throws unexpectedly.
    ///   - Ensure capability flags correctly gate optional operations.
    ///   - Ensure authentication failures surface as clean ValidationFailures.
    ///   - Establish a single, authoritative set of invariants that all adapters
    ///     must honour, reducing the risk of copy-paste drift.
    /// </summary>
    public abstract class DownloadClientContractTests
    {
        // ── Subclass hooks ─────────────────────────────────────────────────────────

        /// <summary>Return the fully-initialised adapter under test.</summary>
        protected abstract IDownloadClientV2 CreateSubject();

        /// <summary>
        /// Configure the subject's dependencies so that <see cref="IDownloadClientV2.GetQueue"/>
        /// returns an empty (but successful) result.
        /// </summary>
        protected abstract void GivenEmptyQueue();

        /// <summary>
        /// Configure the subject's dependencies so that connectivity operations raise
        /// <see cref="DownloadClientAuthenticationException"/>.
        /// </summary>
        protected abstract void GivenAuthenticationFailure();

        // ── Helpers ────────────────────────────────────────────────────────────────

        private IDownloadClientV2 Subject => _subject ??= CreateSubject();
        private IDownloadClientV2 _subject;

        [SetUp]
        public void ContractSetUp()
        {
            _subject = null;
        }

        // ── Contract tests ─────────────────────────────────────────────────────────

        [Test]
        public void GetQueue_should_return_non_null_collection()
        {
            GivenEmptyQueue();

            var result = Subject.GetQueue();

            result.Should().NotBeNull("GetQueue must never return null");
        }

        [Test]
        public void GetQueue_should_not_throw_when_queue_is_empty()
        {
            GivenEmptyQueue();

            Action act = () => Subject.GetQueue();

            act.Should().NotThrow();
        }

        [Test]
        public void Capabilities_should_be_a_valid_flags_value()
        {
            var caps = Subject.Capabilities;
            // Every bit in caps must correspond to a defined enum member.
            var allDefined = (DownloadClientCapabilities)0;
            foreach (DownloadClientCapabilities flag in Enum.GetValues(typeof(DownloadClientCapabilities)))
            {
                allDefined |= flag;
            }

            (caps & ~allDefined).Should().Be(DownloadClientCapabilities.None,
                "Capabilities must not contain undefined bits");
        }

        [Test]
        public void Protocol_should_be_defined_enum_value()
        {
            Enum.IsDefined(typeof(DownloadProtocol), Subject.Protocol)
                .Should().BeTrue("Protocol must be a defined DownloadProtocol");
        }

        [Test]
        public void TestConnectivity_should_handle_auth_failure_gracefully()
        {
            GivenAuthenticationFailure();

            var failures = new List<ValidationFailure>();

            // Should not throw — auth errors must be surfaced as ValidationFailures.
            Action act = () => Subject.TestConnectivity(failures);
            act.Should().NotThrow("auth failures must be caught and returned as ValidationFailure");

            failures.Should().NotBeEmpty("an auth failure must produce at least one ValidationFailure");
        }

        [Test]
        public void Pause_should_throw_NotSupportedException_when_capability_is_absent()
        {
            if (Subject.Capabilities.HasFlag(DownloadClientCapabilities.CanPause))
            {
                Assert.Ignore("Client supports CanPause — skipping absence test.");
                return;
            }

            Action act = () => Subject.Pause("fake-id");
            act.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void Resume_should_throw_NotSupportedException_when_capability_is_absent()
        {
            if (Subject.Capabilities.HasFlag(DownloadClientCapabilities.CanResume))
            {
                Assert.Ignore("Client supports CanResume — skipping absence test.");
                return;
            }

            Action act = () => Subject.Resume("fake-id");
            act.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void SetCategory_should_throw_NotSupportedException_when_capability_is_absent()
        {
            if (Subject.Capabilities.HasFlag(DownloadClientCapabilities.CanSetCategory))
            {
                Assert.Ignore("Client supports CanSetCategory — skipping absence test.");
                return;
            }

            Action act = () => Subject.SetCategory("fake-id", "cat");
            act.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void SetSeedRatio_should_throw_NotSupportedException_when_capability_is_absent()
        {
            if (Subject.Capabilities.HasFlag(DownloadClientCapabilities.CanSetSeedRatio))
            {
                Assert.Ignore("Client supports CanSetSeedRatio — skipping absence test.");
                return;
            }

            Action act = () => Subject.SetSeedRatio("fake-id", 1.0);
            act.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void SetSeedTime_should_throw_NotSupportedException_when_capability_is_absent()
        {
            if (Subject.Capabilities.HasFlag(DownloadClientCapabilities.CanSetSeedTime))
            {
                Assert.Ignore("Client supports CanSetSeedTime — skipping absence test.");
                return;
            }

            Action act = () => Subject.SetSeedTime("fake-id", 60);
            act.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void GetQueue_items_should_have_non_empty_DownloadId()
        {
            GivenEmptyQueue();

            // If the queue is non-empty every item must have an identifier.
            var items = Subject.GetQueue();
            foreach (var item in items)
            {
                item.DownloadId.Should().NotBeNullOrEmpty(
                    "every DownloadQueueItem must carry a non-empty DownloadId");
            }
        }

        [Test]
        public void GetQueue_items_should_have_defined_State()
        {
            GivenEmptyQueue();

            var items = Subject.GetQueue();
            foreach (var item in items)
            {
                Enum.IsDefined(typeof(DownloadQueueItemState), item.State)
                    .Should().BeTrue("item State must be a defined DownloadQueueItemState");
            }
        }
    }
}
