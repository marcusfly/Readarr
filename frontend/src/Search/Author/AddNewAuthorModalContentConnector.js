import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { addAuthor, setAuthorAddDefault } from 'Store/Actions/searchActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createSystemStatusSelector from 'Store/Selectors/createSystemStatusSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import AddNewAuthorModalContent from './AddNewAuthorModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.search,
    (state) => state.settings.metadataProfiles,
    (state) => state.settings.qualityProfiles.items,
    (state) => state.settings.rootFolders.items,
    createDimensionsSelector(),
    createSystemStatusSelector(),
    (searchState, metadataProfiles, qualityProfileItems, rootFolderItems, dimensions, systemStatus) => {
      const {
        isAdding,
        addError,
        authorDefaults
      } = searchState;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(authorDefaults, {}, addError);

      const fallbackRootFolderPath = rootFolderItems[0] ? rootFolderItems[0].path : '';
      const fallbackQualityProfileId = qualityProfileItems[0] ? qualityProfileItems[0].id : 0;
      const selectedQualityProfileId = parseInt(settings.qualityProfileId.value);
      const fallbackMetadataProfileId = metadataProfiles.items[0] ? metadataProfiles.items[0].id : 0;
      const selectedMetadataProfileId = parseInt(settings.metadataProfileId.value);

      const selectedRootFolderPath = settings.rootFolderPath.value || '';
      const selectedAudiobookRootFolderPath = settings.audiobookRootFolderPath ? settings.audiobookRootFolderPath.value : '';
      const normalizedRootFolderPath = selectedRootFolderPath || fallbackRootFolderPath;
      const normalizedAudiobookRootFolderPath = selectedAudiobookRootFolderPath || normalizedRootFolderPath;
      const normalizedQualityProfileId = (!selectedQualityProfileId || selectedQualityProfileId < 0) ? fallbackQualityProfileId : selectedQualityProfileId;
      const normalizedMetadataProfileId = (!selectedMetadataProfileId || selectedMetadataProfileId < 0) ? fallbackMetadataProfileId : selectedMetadataProfileId;

      return {
        isAdding,
        addError,
        showMetadataProfile: metadataProfiles.items.length > 2, // NONE (not allowed for authors) and one other
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        isWindows: systemStatus.isWindows,
        isAddDisabled: !normalizedRootFolderPath || !normalizedQualityProfileId || !normalizedMetadataProfileId,
        fallbackRootFolderPath,
        fallbackAudiobookRootFolderPath: normalizedAudiobookRootFolderPath,
        fallbackQualityProfileId,
        fallbackMetadataProfileId,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  setAuthorAddDefault,
  addAuthor
};

class AddNewAuthorModalContentConnector extends Component {

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setAuthorAddDefault({ [name]: value });
  };

  onAddAuthorPress = (searchForMissingBooks) => {
    const {
      foreignAuthorId,
      rootFolderPath,
      audiobookRootFolderPath,
      monitor,
      monitorNewItems,
      qualityProfileId,
      metadataProfileId,
      tags,
      fallbackRootFolderPath,
      fallbackAudiobookRootFolderPath,
      fallbackQualityProfileId,
      fallbackMetadataProfileId
    } = this.props;

    const selectedRootFolderPath = rootFolderPath ? rootFolderPath.value : '';
    const selectedAudiobookRootFolderPath = audiobookRootFolderPath ? audiobookRootFolderPath.value : '';
    const selectedQualityProfileId = parseInt(qualityProfileId.value);
    const selectedMetadataProfileId = parseInt(metadataProfileId.value);

    const normalizedRootFolderPath = selectedRootFolderPath || fallbackRootFolderPath;
    const normalizedAudiobookRootFolderPath = selectedAudiobookRootFolderPath || fallbackAudiobookRootFolderPath || normalizedRootFolderPath;
    const normalizedQualityProfileId = (!selectedQualityProfileId || selectedQualityProfileId < 0) ? fallbackQualityProfileId : selectedQualityProfileId;
    const normalizedMetadataProfileId = (!selectedMetadataProfileId || selectedMetadataProfileId < 0) ? fallbackMetadataProfileId : selectedMetadataProfileId;

    if (!normalizedRootFolderPath || !normalizedQualityProfileId || !normalizedMetadataProfileId) {
      return;
    }

    this.props.addAuthor({
      foreignAuthorId,
      rootFolderPath: normalizedRootFolderPath,
      audiobookRootFolderPath: normalizedAudiobookRootFolderPath,
      monitor: monitor.value,
      monitorNewItems: monitorNewItems.value,
      qualityProfileId: normalizedQualityProfileId,
      metadataProfileId: normalizedMetadataProfileId,
      tags: tags.value,
      searchForMissingBooks
    });
  };

  //
  // Render

  render() {
    return (
      <AddNewAuthorModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onAddAuthorPress={this.onAddAuthorPress}
      />
    );
  }
}

AddNewAuthorModalContentConnector.propTypes = {
  foreignAuthorId: PropTypes.string.isRequired,
  rootFolderPath: PropTypes.object,
  audiobookRootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  monitorNewItems: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  isAddDisabled: PropTypes.bool.isRequired,
  fallbackRootFolderPath: PropTypes.string,
  fallbackAudiobookRootFolderPath: PropTypes.string,
  fallbackQualityProfileId: PropTypes.number,
  fallbackMetadataProfileId: PropTypes.number,
  tags: PropTypes.object.isRequired,
  onModalClose: PropTypes.func.isRequired,
  setAuthorAddDefault: PropTypes.func.isRequired,
  addAuthor: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewAuthorModalContentConnector);
