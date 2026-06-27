using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Magazines.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Handlers
{
    [TestFixture]
    public class MagazineAddedHandlerFixture : CoreTest<MagazineAddedHandler>
    {
        [Test]
        public void should_queue_title_level_magazine_search_when_add_requests_immediate_search()
        {
            var magazine = new Magazine
            {
                Id = 6,
                Title = "Playboy",
                AddOptions = new AddMagazineOptions
                {
                    SearchForMissingIssues = true,
                    Monitor = MonitorTypes.All
                }
            };

            Subject.Handle(new MagazineAddedEvent(magazine));

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push(It.Is<MagazineSearchCommand>(c => c.MagazineId == 6)), Times.Once());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push(It.IsAny<MissingMagazineIssueSearchCommand>()), Times.Never());
        }
    }
}
