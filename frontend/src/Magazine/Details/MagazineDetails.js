import PropTypes from 'prop-types';
import React, { Component } from 'react';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import { icons } from 'Helpers/Props';
import DeleteMagazineModal from 'Magazine/Delete/DeleteMagazineModal';
import EditMagazineModal from 'Magazine/Edit/EditMagazineModal';
import translate from 'Utilities/String/translate';
import MagazineDetailsHeader from './MagazineDetailsHeader';
import MagazineIssueGraphic from './MagazineIssueGraphic';
import styles from './MagazineDetails.css';

function getIssueSortValue(issue) {
  return Date.UTC(
    issue.issueYear || 0,
    Math.max((issue.issueMonth || 1) - 1, 0),
    issue.issueDay || 1
  );
}

function formatIssue(issue) {
  const month = `${issue.issueMonth}`.padStart(2, '0');
  const day = issue.issueDay == null ? '' : `-${`${issue.issueDay}`.padStart(2, '0')}`;
  return `${issue.issueYear}-${month}${day}`;
}

class MagazineDetails extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isEditMagazineModalOpen: false,
      isDeleteMagazineModalOpen: false
    };
  }

  onEditMagazinePress = () => {
    this.setState({ isEditMagazineModalOpen: true });
  };

  onEditMagazineModalClose = () => {
    this.setState({ isEditMagazineModalOpen: false });
  };

  onDeleteMagazinePress = () => {
    this.setState({
      isEditMagazineModalOpen: false,
      isDeleteMagazineModalOpen: true
    });
  };

  onDeleteMagazineModalClose = () => {
    this.setState({ isDeleteMagazineModalOpen: false });
  };

  render() {
    const {
      isFetching,
      magazine,
      issues = [],
      isRefreshing,
      isSearching,
      onSearch,
      onRefresh,
      onMonitorChange,
      onMonitorTogglePress
    } = this.props;

    if (isFetching && !magazine) {
      return (
        <PageContent title={translate('Loading')}>
          <PageContentBody innerClassName={styles.innerContentBody}>
            <LoadingIndicator />
          </PageContentBody>
        </PageContent>
      );
    }

    if (!magazine) {
      return (
        <PageContent title="Magazine Not Found">
          <PageContentBody innerClassName={styles.innerContentBody}>
            <div className={styles.emptyState}>
              Magazine not found.
            </div>
          </PageContentBody>
        </PageContent>
      );
    }

    const sortedIssues = [...issues].sort((left, right) => getIssueSortValue(right) - getIssueSortValue(left));
    const latestIssue = sortedIssues[0] || null;
    const monitoredIssueCount = sortedIssues.filter((issue) => issue.monitored).length;
    const fileIssueCount = sortedIssues.filter((issue) => issue.hasFile).length;
    const statistics = magazine.statistics || {};

    return (
      <PageContent title={magazine.title}>
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('Refresh')}
              iconName={icons.REFRESH}
              spinningName={icons.REFRESH}
              title="Refresh magazine information and rescan files"
              isSpinning={isRefreshing}
              onPress={onRefresh}
            />

            <PageToolbarButton
              label="Search Magazine"
              iconName={icons.SEARCH}
              isSpinning={isSearching}
              onPress={onSearch}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('Edit')}
              iconName={icons.EDIT}
              onPress={this.onEditMagazinePress}
            />

            <PageToolbarButton
              label={translate('Delete')}
              iconName={icons.DELETE}
              onPress={this.onDeleteMagazinePress}
            />
          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody innerClassName={styles.innerContentBody}>
          <MagazineDetailsHeader
            width={this.props.width}
            magazine={magazine}
            latestIssue={latestIssue}
            monitoredIssueCount={monitoredIssueCount}
            fileIssueCount={fileIssueCount}
            onMonitorTogglePress={onMonitorTogglePress}
          />

          <div className={styles.contentContainer}>
            <div className={styles.summaryGrid}>
              <div className={styles.summaryCard}>
                <div className={styles.summaryLabel}>Publisher</div>
                <div className={styles.summaryValue}>{magazine.publisher || 'Unknown'}</div>
              </div>

              <div className={styles.summaryCard}>
                <div className={styles.summaryLabel}>Language</div>
                <div className={styles.summaryValue}>{magazine.language || 'Not available'}</div>
              </div>

              <div className={styles.summaryCard}>
                <div className={styles.summaryLabel}>Country</div>
                <div className={styles.summaryValue}>{magazine.country || 'Not available'}</div>
              </div>

              <div className={styles.summaryCard}>
                <div className={styles.summaryLabel}>Library Path</div>
                <div className={styles.summaryValue}>{magazine.path || 'Not set'}</div>
              </div>

              <div className={styles.summaryCard}>
                <div className={styles.summaryLabel}>Issues Tracked</div>
                <div className={styles.summaryValue}>{statistics.issueCount || sortedIssues.length}</div>
              </div>

              <div className={styles.summaryCard}>
                <div className={styles.summaryLabel}>Files Imported</div>
                <div className={styles.summaryValue}>{statistics.issueFileCount || fileIssueCount}</div>
              </div>
            </div>

            <div className={styles.tableSection}>
              <div className={styles.sectionTitle}>Issues</div>

              {
                !sortedIssues.length &&
                  <div className={styles.emptyState}>
                    No issues tracked yet. Use Search Magazine to pull in recent releases.
                  </div>
              }

              {
                !!sortedIssues.length &&
                  <table className={styles.issuesTable}>
                    <thead>
                      <tr>
                        <th className={styles.thumbnailColumn}>Cover</th>
                        <th>Issue</th>
                        <th>Title</th>
                        <th>Monitored</th>
                        <th>Has File</th>
                        <th>Quality</th>
                      </tr>
                    </thead>
                    <tbody>
                      {
                        sortedIssues.map((issue) => (
                          <tr key={issue.id}>
                            <td className={styles.thumbnailColumn}>
                              <MagazineIssueGraphic images={issue.images} />
                            </td>
                            <td>{formatIssue(issue)}</td>
                            <td>{issue.releaseTitle || issue.issueNumber || '-'}</td>
                            <td>
                              <input
                                type="checkbox"
                                checked={issue.monitored}
                                onChange={(event) => onMonitorChange(issue.id, event.target.checked)}
                              />
                            </td>
                            <td>{issue.hasFile ? 'Yes' : 'No'}</td>
                            <td>{issue.quality?.quality?.name || issue.quality?.quality?.id || '-'}</td>
                          </tr>
                        ))
                      }
                    </tbody>
                  </table>
              }
            </div>
          </div>

          <EditMagazineModal
            isOpen={this.state.isEditMagazineModalOpen}
            magazineId={magazine.id}
            onModalClose={this.onEditMagazineModalClose}
          />

          <DeleteMagazineModal
            isOpen={this.state.isDeleteMagazineModalOpen}
            magazineId={magazine.id}
            onModalClose={this.onDeleteMagazineModalClose}
          />
        </PageContentBody>
      </PageContent>
    );
  }
}

MagazineDetails.propTypes = {
  width: PropTypes.number.isRequired,
  isFetching: PropTypes.bool,
  isRefreshing: PropTypes.bool.isRequired,
  isSearching: PropTypes.bool.isRequired,
  magazine: PropTypes.object,
  issues: PropTypes.arrayOf(PropTypes.object),
  onMonitorChange: PropTypes.func.isRequired,
  onMonitorTogglePress: PropTypes.func.isRequired,
  onRefresh: PropTypes.func.isRequired,
  onSearch: PropTypes.func.isRequired
};

MagazineDetails.defaultProps = {
  isFetching: false,
  magazine: null,
  issues: []
};

export default MagazineDetails;
