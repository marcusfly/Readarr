import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchMagazineRootFolders } from 'Store/Actions/magazineRootFolderActions';
import { clearSearchResults, getSearchResults } from 'Store/Actions/searchActions';
import { fetchRootFolders } from 'Store/Actions/settingsActions';
import parseUrl from 'Utilities/String/parseUrl';
import AddNewItem from './AddNewItem';
import { normalizeSearchScope } from './searchScopes';

function createMapStateToProps() {
  return createSelector(
    (state) => state.search,
    (state) => state.authors.items.length,
    (state) => state.router.location,
    (state) => state.settings.ui.item,
    (search, existingAuthorsCount, location, uiSettings) => {
      const { params } = parseUrl(location.search);

      return {
        ...search,
        term: params.term,
        initialSearchScope: normalizeSearchScope(params.scope),
        searchWhileTyping: uiSettings.searchWhileTyping === true,
        hasExistingAuthors: existingAuthorsCount > 0
      };
    }
  );
}

const mapDispatchToProps = {
  getSearchResults,
  clearSearchResults,
  fetchRootFolders,
  fetchMagazineRootFolders
};

class AddNewItemConnector extends Component {
  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchRootFolders();
    this.props.fetchMagazineRootFolders();
  }

  componentWillUnmount() {
    this.props.clearSearchResults();
  }

  //
  // Listeners

  onSearchChange = (term, scope) => {
    if (term === '') {
      this.props.clearSearchResults();
    } else {
      this.props.getSearchResults({ term, scope });
    }
  };

  onClearSearch = () => {
    this.props.clearSearchResults();
  };

  //
  // Render

  render() {
    const {
      term,
      ...otherProps
    } = this.props;

    return (
      <AddNewItem
        term={term}
        {...otherProps}
        onSearchChange={this.onSearchChange}
        onClearSearch={this.onClearSearch}
      />
    );
  }
}

AddNewItemConnector.propTypes = {
  term: PropTypes.string,
  initialSearchScope: PropTypes.string.isRequired,
  searchWhileTyping: PropTypes.bool,
  getSearchResults: PropTypes.func.isRequired,
  clearSearchResults: PropTypes.func.isRequired,
  fetchRootFolders: PropTypes.func.isRequired,
  fetchMagazineRootFolders: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewItemConnector);
