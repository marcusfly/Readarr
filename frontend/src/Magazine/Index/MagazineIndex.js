import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import monitorOptions from 'Utilities/Magazine/monitorOptions';

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
      currentPage: 1,
      rootFolderName: '',
      rootFolderPath: ''
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

  onRootFolderFieldChange = (event) => {
    const { name, value } = event.target;

    this.setState({
      [name]: value
    });
  };

  onAddRootFolder = (event) => {
    event.preventDefault();

    const {
      rootFolderName,
      rootFolderPath
    } = this.state;

    if (!rootFolderPath.trim()) {
      return;
    }

    this.props.saveMagazineRootFolder({
      name: rootFolderName.trim(),
      path: rootFolderPath.trim(),
      defaultQualityProfileId: this.props.defaultQualityProfileId,
      defaultMetadataProfileId: this.props.defaultMetadataProfileId,
      defaultMonitorOption: monitorOptions[0].key,
      defaultTags: []
    });

    this.setState({
      rootFolderName: '',
      rootFolderPath: ''
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

    const sortedItems = [...filteredItems].sort((left, right) => {
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

    return sortedItems;
  }

  render() {
    const {
      isFetching,
      rootFolders = [],
      deleteMagazineRootFolder
    } = this.props;

    const {
      filterText,
      monitoredFilter,
      sortKey,
      sortDirection,
      currentPage,
      rootFolderName,
      rootFolderPath
    } = this.state;

    const filteredItems = this.getFilteredItems();
    const totalPages = Math.max(Math.ceil(filteredItems.length / PAGE_SIZE), 1);
    const safePage = Math.min(currentPage, totalPages);
    const startIndex = (safePage - 1) * PAGE_SIZE;
    const pageItems = filteredItems.slice(startIndex, startIndex + PAGE_SIZE);

    if (isFetching && !filteredItems.length) {
      return <div>Loading magazines...</div>;
    }

    return (
      <div className="page-content">
        <h2>Magazines</h2>

        <section style={{ marginBottom: '2rem' }}>
          <h3>Magazine Root Folders</h3>

          <form onSubmit={this.onAddRootFolder} style={{ marginBottom: '1rem' }}>
            <input
              type="text"
              name="rootFolderName"
              placeholder="Display name"
              value={rootFolderName}
              onChange={this.onRootFolderFieldChange}
              style={{ marginRight: '0.5rem' }}
            />

            <input
              type="text"
              name="rootFolderPath"
              placeholder="/magazines"
              value={rootFolderPath}
              onChange={this.onRootFolderFieldChange}
              style={{ marginRight: '0.5rem', minWidth: '18rem' }}
            />

            <button type="submit" disabled={!rootFolderPath.trim()}>
              Add Root Folder
            </button>
          </form>

          {
            rootFolders.length ?
              <ul>
                {
                  rootFolders.map((rootFolder) => {
                    return (
                      <li key={rootFolder.id} style={{ marginBottom: '0.5rem' }}>
                        <strong>{rootFolder.name || 'Magazine Root'}</strong> {rootFolder.path}
                        {' '}
                        <button type="button" onClick={() => deleteMagazineRootFolder({ id: rootFolder.id })}>
                          Remove
                        </button>
                      </li>
                    );
                  })
                }
              </ul> :
              <p>No magazine root folders have been configured yet.</p>
          }
        </section>

        <section style={{ marginBottom: '1rem' }}>
          <input
            type="text"
            placeholder="Filter by title or publisher"
            value={filterText}
            onChange={this.onFilterTextChange}
            style={{ marginRight: '0.5rem' }}
          />

          <select value={monitoredFilter} onChange={this.onMonitoredFilterChange}
            style={{ marginRight: '0.5rem' }}
          >
            <option value="all">All magazines</option>
            <option value="monitored">Monitored only</option>
            <option value="unmonitored">Unmonitored only</option>
            <option value="missing">Missing issues</option>
          </select>

          <select value={sortKey} onChange={this.onSortKeyChange}
            style={{ marginRight: '0.5rem' }}
          >
            <option value="title">Sort by title</option>
            <option value="publisher">Sort by publisher</option>
            <option value="monitored">Sort by monitored</option>
            <option value="issueCount">Sort by issue count</option>
            <option value="issueFileCount">Sort by file count</option>
          </select>

          <select value={sortDirection} onChange={this.onSortDirectionChange}>
            <option value="asc">Ascending</option>
            <option value="desc">Descending</option>
          </select>
        </section>

        {
          filteredItems.length ?
            <React.Fragment>
              <table>
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
                            <Link to={`/magazine/${magazine.id}`}>{magazine.title}</Link>
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

              <div style={{ marginTop: '1rem' }}>
                <button type="button" onClick={() => this.onPageChange(-1)}
                  disabled={safePage <= 1}
                >
                  Previous
                </button>
                {' '}
                <span>Page {safePage} of {totalPages}</span>
                {' '}
                <button type="button" onClick={() => this.onPageChange(1)}
                  disabled={safePage >= totalPages}
                >
                  Next
                </button>
              </div>
            </React.Fragment> :
            <p>No magazines match the current filters.</p>
        }
      </div>
    );
  }
}

MagazineIndex.propTypes = {
  isFetching: PropTypes.bool,
  items: PropTypes.arrayOf(PropTypes.object),
  rootFolders: PropTypes.arrayOf(PropTypes.object),
  defaultQualityProfileId: PropTypes.number.isRequired,
  defaultMetadataProfileId: PropTypes.number.isRequired,
  saveMagazineRootFolder: PropTypes.func.isRequired,
  deleteMagazineRootFolder: PropTypes.func.isRequired
};

export default MagazineIndex;
