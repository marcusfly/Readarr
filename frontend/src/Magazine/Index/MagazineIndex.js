import PropTypes from 'prop-types';
import React, { Component } from 'react';
import LoadingPage from 'Components/Page/LoadingPage';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import { align, icons } from 'Helpers/Props';
import { buildAddNewSearchPath, searchScopes } from 'Search/searchScopes';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import NoMagazine from './NoMagazine';
import MagazineIndexPosters from './Posters/MagazineIndexPosters';
import styles from './MagazineIndex.css';

function compareValues(left, right) {
  if (typeof left === 'string' || typeof right === 'string') {
    return String(left || '').localeCompare(String(right || ''));
  }

  return (left || 0) - (right || 0);
}

class MagazineIndex extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      filterText: '',
      monitoredFilter: 'all',
      sortKey: 'title',
      sortDirection: 'asc'
    };
  }

  onFilterTextChange = (event) => {
    this.setState({
      filterText: event.target.value
    });
  };

  onMonitoredFilterChange = (event) => {
    this.setState({
      monitoredFilter: event.target.value
    });
  };

  onSortKeyChange = (event) => {
    this.setState({
      sortKey: event.target.value
    });
  };

  onSortDirectionChange = (event) => {
    this.setState({
      sortDirection: event.target.value
    });
  };

  getFilteredItems() {
    const {
      items = []
    } = this.props;

    const {
      filterText,
      monitoredFilter,
      sortKey,
      sortDirection
    } = this.state;

    const normalizedFilter = filterText.trim().toLowerCase();

    const filteredItems = items.filter((magazine) => {
      const statistics = magazine.statistics || {};
      const matchesText = !normalizedFilter ||
        [magazine.title, magazine.publisher, magazine.cleanTitle]
          .filter(Boolean)
          .some((value) => value.toLowerCase().includes(normalizedFilter));

      if (!matchesText) {
        return false;
      }

      if (monitoredFilter === 'monitored') {
        return magazine.monitored;
      }

      if (monitoredFilter === 'unmonitored') {
        return !magazine.monitored;
      }

      if (monitoredFilter === 'missing') {
        return (statistics.issueCount || 0) > (statistics.issueFileCount || 0);
      }

      return true;
    });

    return [...filteredItems].sort((left, right) => {
      const leftStatistics = left.statistics || {};
      const rightStatistics = right.statistics || {};
      let comparison = 0;

      switch (sortKey) {
        case 'publisher':
          comparison = compareValues(left.publisher, right.publisher);
          break;
        case 'issueCount':
          comparison = compareValues(leftStatistics.issueCount, rightStatistics.issueCount);
          break;
        case 'issueFileCount':
          comparison = compareValues(leftStatistics.issueFileCount, rightStatistics.issueFileCount);
          break;
        case 'monitored':
          comparison = compareValues(left.monitored ? 1 : 0, right.monitored ? 1 : 0);
          break;
        default:
          comparison = compareValues(left.title, right.title);
          break;
      }

      return sortDirection === 'desc' ? comparison * -1 : comparison;
    });
  }

  render() {
    const {
      error,
      isFetching,
      isRefreshingMagazines,
      isPopulated,
      items = [],
      onRefreshPress
    } = this.props;

    const {
      filterText,
      monitoredFilter,
      sortKey,
      sortDirection
    } = this.state;

    const filteredItems = this.getFilteredItems();
    const totalItems = items.length;
    const hasNoMagazines = !totalItems;

    if (isFetching && !isPopulated) {
      return (
        <LoadingPage />
      );
    }

    return (
      <PageContent title="Magazines">
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label="Update All"
              iconName={icons.REFRESH}
              spinningName={icons.REFRESH}
              isSpinning={isRefreshingMagazines}
              onPress={onRefreshPress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label="Add Root Folder"
              iconName={icons.FOLDER_OPEN}
              to="/settings/mediamanagement"
            />

            <PageToolbarButton
              label="Add New Magazine"
              iconName={icons.ADD}
              to={buildAddNewSearchPath(searchScopes.MAGAZINES)}
            />
          </PageToolbarSection>

          <PageToolbarSection alignContent={align.RIGHT}>
            <PageToolbarButton
              label="Root Folders"
              iconName={icons.FOLDER}
              to="/settings/mediamanagement"
            />
          </PageToolbarSection>
        </PageToolbar>

        <div className={styles.pageContentBodyWrapper}>
          <PageContentBody
            className={styles.contentBody}
            innerClassName={styles.innerContentBody}
          >
            {
              !isFetching && !!error &&
                <div className={styles.errorMessage}>
                  {getErrorMessage(error, 'Failed to load magazines from API')}
                </div>
            }

            {
              !error && isPopulated && !!totalItems &&
                <div className={styles.contentBodyContainer}>
                  <div className={styles.filtersPanel}>
                    <input
                      className={styles.textInput}
                      type="text"
                      placeholder="Filter by title or publisher"
                      value={filterText}
                      onChange={this.onFilterTextChange}
                    />

                    <select
                      className={styles.selectInput}
                      value={monitoredFilter}
                      onChange={this.onMonitoredFilterChange}
                    >
                      <option value="all">All magazines</option>
                      <option value="monitored">Monitored only</option>
                      <option value="unmonitored">Unmonitored only</option>
                      <option value="missing">Missing issues</option>
                    </select>

                    <select
                      className={styles.selectInput}
                      value={sortKey}
                      onChange={this.onSortKeyChange}
                    >
                      <option value="title">Sort by title</option>
                      <option value="publisher">Sort by publisher</option>
                      <option value="monitored">Sort by monitored</option>
                      <option value="issueCount">Sort by issue count</option>
                      <option value="issueFileCount">Sort by file count</option>
                    </select>

                    <select
                      className={styles.selectInput}
                      value={sortDirection}
                      onChange={this.onSortDirectionChange}
                    >
                      <option value="asc">Ascending</option>
                      <option value="desc">Descending</option>
                    </select>
                  </div>

                  {
                    filteredItems.length ?
                      <>
                        <div className={styles.resultsSummary}>
                          Showing {filteredItems.length} of {totalItems} magazines
                        </div>

                        <MagazineIndexPosters items={filteredItems} />
                      </> :
                      <MagazineIndexPosters
                        items={[]}
                        emptyMessage="All magazines are hidden by the current filter."
                      />
                  }
                </div>
            }

            {
              !error && isPopulated && hasNoMagazines &&
                <div className={styles.contentBodyContainer}>
                  <NoMagazine totalItems={totalItems} />
                </div>
            }
          </PageContentBody>
        </div>
      </PageContent>
    );
  }
}

MagazineIndex.propTypes = {
  error: PropTypes.object,
  isFetching: PropTypes.bool,
  isRefreshingMagazines: PropTypes.bool,
  isPopulated: PropTypes.bool.isRequired,
  items: PropTypes.arrayOf(PropTypes.object),
  onRefreshPress: PropTypes.func.isRequired
};

MagazineIndex.defaultProps = {
  isFetching: false,
  isRefreshingMagazines: false,
  items: [],
  error: null
};

export default MagazineIndex;
