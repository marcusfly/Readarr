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
import {
  createMagazineIssueYearGroups,
  getDefaultExpandedYearState,
  getMagazineIssueSortValue
} from './createMagazineIssueYearGroups';
import MagazineDetailsHeader from './MagazineDetailsHeader';
import MagazineIssueYearGroup from './MagazineIssueYearGroup';
import styles from './MagazineDetails.css';

class MagazineDetails extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isEditMagazineModalOpen: false,
      isDeleteMagazineModalOpen: false,
      expandedYears: {}
    };
  }

  componentDidMount() {
    this.setExpandedYears(this.props.issues || []);
  }

  componentDidUpdate(prevProps) {
    if (prevProps.issues !== this.props.issues) {
      this.setExpandedYears(this.props.issues || []);
    }
  }

  setExpandedYears(issues) {
    const groupedIssues = createMagazineIssueYearGroups(issues);

    this.setState((state) => {
      const nextExpandedYears = {};
      let hasExistingExpandedYear = false;

      groupedIssues.forEach((group) => {
        const existingValue = state.expandedYears[group.year];

        if (typeof existingValue === 'boolean') {
          nextExpandedYears[group.year] = existingValue;
          hasExistingExpandedYear = hasExistingExpandedYear || existingValue;
          return;
        }

        nextExpandedYears[group.year] = false;
      });

      if (!hasExistingExpandedYear && groupedIssues.length) {
        return {
          expandedYears: getDefaultExpandedYearState(groupedIssues)
        };
      }

      return {
        expandedYears: nextExpandedYears
      };
    });
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

  onYearExpandPress = (yearKey, nextExpanded) => {
    this.setState((state) => ({
      expandedYears: {
        ...state.expandedYears,
        [yearKey]: typeof nextExpanded === 'boolean' ? nextExpanded : !state.expandedYears[yearKey]
      }
    }));
  };

  onExpandAllPress = () => {
    this.setState((state) => {
      const yearKeys = Object.keys(state.expandedYears);
      const areAllExpanded = yearKeys.every((key) => state.expandedYears[key]);
      const expandedYears = {};

      yearKeys.forEach((key) => {
        expandedYears[key] = !areAllExpanded;
      });

      return { expandedYears };
    });
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

    const sortedIssues = [...issues].sort((left, right) => getMagazineIssueSortValue(right) - getMagazineIssueSortValue(left));
    const groupedIssues = createMagazineIssueYearGroups(sortedIssues);
    const latestIssue = sortedIssues[0] || null;
    const monitoredIssueCount = sortedIssues.filter((issue) => issue.monitored).length;
    const fileIssueCount = sortedIssues.filter((issue) => issue.hasFile).length;
    const statistics = magazine.statistics || {};
    const expandedYearCount = groupedIssues.filter((group) => this.state.expandedYears[group.year]).length;
    const areAllYearsExpanded = groupedIssues.length > 0 && expandedYearCount === groupedIssues.length;

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
              <div className={styles.sectionHeader}>
                <div className={styles.sectionTitle}>Issues</div>

                {
                  !!groupedIssues.length &&
                    <button
                      className={styles.expandAllButton}
                      type="button"
                      onClick={this.onExpandAllPress}
                    >
                      {areAllYearsExpanded ? 'Collapse All' : 'Expand All'}
                    </button>
                }
              </div>

              {
                !sortedIssues.length &&
                  <div className={styles.emptyState}>
                    No issues tracked yet. Use Search Magazine to pull in recent releases.
                  </div>
              }

              {
                !!sortedIssues.length &&
                  <div className={styles.yearGroups}>
                    {
                      groupedIssues.map((group) => (
                        <MagazineIssueYearGroup
                          key={group.year}
                          group={group}
                          isExpanded={!!this.state.expandedYears[group.year]}
                          onExpandPress={this.onYearExpandPress}
                          onMonitorChange={onMonitorChange}
                        />
                      ))
                    }
                  </div>
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
