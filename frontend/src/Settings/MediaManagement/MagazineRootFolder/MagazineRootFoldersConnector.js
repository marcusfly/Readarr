import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import {
  deleteMagazineRootFolder,
  fetchMagazineRootFolders
} from 'Store/Actions/magazineRootFolderActions';
import MagazineRootFolders from './MagazineRootFolders';

function createMapStateToProps() {
  return createSelector(
    (state) => state.magazineRootFolders,
    (rootFolders) => rootFolders
  );
}

const mapDispatchToProps = {
  dispatchFetchMagazineRootFolders: fetchMagazineRootFolders,
  dispatchDeleteMagazineRootFolder: deleteMagazineRootFolder
};

class MagazineRootFoldersConnector extends Component {
  componentDidMount() {
    this.props.dispatchFetchMagazineRootFolders();
  }

  onConfirmDeleteRootFolder = (id) => {
    this.props.dispatchDeleteMagazineRootFolder({ id });
  };

  render() {
    return (
      <MagazineRootFolders
        {...this.props}
        onConfirmDeleteRootFolder={this.onConfirmDeleteRootFolder}
      />
    );
  }
}

MagazineRootFoldersConnector.propTypes = {
  dispatchFetchMagazineRootFolders: PropTypes.func.isRequired,
  dispatchDeleteMagazineRootFolder: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(MagazineRootFoldersConnector);
