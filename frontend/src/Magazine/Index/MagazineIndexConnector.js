import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { fetchMagazines } from 'Store/Actions/magazineActions';
import {
  deleteMagazineRootFolder,
  fetchMagazineRootFolders,
  saveMagazineRootFolder
} from 'Store/Actions/magazineRootFolderActions';
import MagazineIndex from './MagazineIndex';

class MagazineIndexConnector extends Component {
  componentDidMount() {
    if (!this.props.isPopulated && !this.props.isFetching) {
      this.props.fetchMagazines();
    }

    if (!this.props.areRootFoldersPopulated && !this.props.areRootFoldersFetching) {
      this.props.fetchMagazineRootFolders();
    }
  }

  render() {
    return <MagazineIndex {...this.props} />;
  }
}

MagazineIndexConnector.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  areRootFoldersFetching: PropTypes.bool.isRequired,
  areRootFoldersPopulated: PropTypes.bool.isRequired,
  defaultQualityProfileId: PropTypes.number.isRequired,
  defaultMetadataProfileId: PropTypes.number.isRequired,
  fetchMagazines: PropTypes.func.isRequired,
  fetchMagazineRootFolders: PropTypes.func.isRequired,
  saveMagazineRootFolder: PropTypes.func.isRequired,
  deleteMagazineRootFolder: PropTypes.func.isRequired
};

function mapStateToProps(state) {
  const qualityProfiles = state.settings.qualityProfiles.items;
  const metadataProfiles = state.settings.metadataProfiles.items;

  return {
    ...state.magazines,
    rootFolders: state.magazineRootFolders.items,
    areRootFoldersFetching: state.magazineRootFolders.isFetching,
    areRootFoldersPopulated: state.magazineRootFolders.isPopulated,
    defaultQualityProfileId: qualityProfiles[0] ? qualityProfiles[0].id : 0,
    defaultMetadataProfileId: metadataProfiles[0] ? metadataProfiles[0].id : 0
  };
}

export default connect(mapStateToProps, {
  fetchMagazines,
  fetchMagazineRootFolders,
  saveMagazineRootFolder,
  deleteMagazineRootFolder
})(MagazineIndexConnector);
