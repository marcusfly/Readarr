import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import { align, icons } from 'Helpers/Props';
import { buildAddNewSearchPath, searchScopes } from 'Search/searchScopes';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import MagazineGraphic from './MagazineGraphic';
import NoMagazine from './NoMagazine';
import styles from './MagazineIndex.css';

const PAGE_SIZE = 20;

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
      sortDirection: 'asc',
      currentPage: 1
    };
  }

  onFilterTextChange = (event) => {
    this.setState({
      filterText: event.target.value,
      currentPage: 1
    });
  };

  onMonitoredFilterChange = (event) => {
    this.setState({
      monitoredFilter: event.target.value,
      currentPage: 1
    });
  };

  onSortKeyChange = (event) => {
    this.setState({
      sortKey: event.target.value,
      currentPage: 1
    });
  };

  onSortDirectionChange = (event) => {
    this.setState({
      sortDirection: event.target.value,
      currentPage: 1
    });
  };

  onPageChange = (direction) => {
    this.setState((prevState) => {
      return {
        currentPage: Math.max(prevState.currentPage + direction, 1)
      };
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
      sortDirection,
      currentPage
    } = this.state;

    const filteredItems = this.getFilteredItems();
    const totalItems = items.length;
    const totalPages = Math.max(Math.ceil(filteredItems.length / PAGE_SIZE), 1);
    const safePage = Math.min(currentPage, totalPages);
    const startIndex = (safePage - 1) * PAGE_SIZE;
    const pageItems = filteredItems.slice(startIndex, startIndex + PAGE_SIZE);
    const hasNoMagazines = !totalItems;

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
              isFetching && !isPopulated &&
                <LoadingIndicator />
            }

            {
              !isFetching && !!error &&
                <div className={styles.errorMessage}>
                  {getErrorMessage(error, 'Failed to load magazines from API')}
                </div>
            }

            {
              !error && isPopulated && !!totalItems &&
                <div className={styles.contentBodyContainer}>
                  <div className={styles.filters}>
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
                    pageItems.length ?
                      <div className={styles.tableWrapper}>
                        <table className={styles.table}>
                          <thead>
                            <tr>
                              <th>Title</th>
                              <th>Publisher</th>
                              <th>Monitored</th>
                              <th>Issues</th>
                              <th>Files</th>
                            </tr>
                          </thead>
                          <tbody>
                            {
                              pageItems.map((magazine) => {
                                const statistics = magazine.statistics || {};

                                return (
                                  <tr key={magazine.id}>
                                    <td>
                                      <div className={styles.titleCell}>
                                        <MagazineGraphic title={magazine.title} images={magazine.images} />

                                        <Link className={styles.tableLink} to={`/magazine/${magazine.id}`}>
                                          {magazine.title}
                                        </Link>
                                      </div>
                                    </td>
                                    <td>{magazine.publisher || 'Unknown'}</td>
                                    <td>{magazine.monitored ? 'Yes' : 'No'}</td>
                                    <td>{statistics.issueCount || 0}</td>
                                    <td>{statistics.issueFileCount || 0}</td>
                                  </tr>
                                );
                              })
                            }
                          </tbody>
                        </table>
                      </div> :
                      <NoMagazine totalItems={totalItems} />
                  }

                  {
                    pageItems.length ?
                      <div className={styles.pagination}>
                        <Button
                          isDisabled={safePage <= 1}
                          onPress={() => this.onPageChange(-1)}
                        >
                          Previous
                        </Button>

                        <div className={styles.pageIndicator}>
                          Page {safePage} of {totalPages}
                        </div>

                        <Button
                          isDisabled={safePage >= totalPages}
                          onPress={() => this.onPageChange(1)}
                        >
                          Next
                        </Button>
                      </div> :
                      null
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
