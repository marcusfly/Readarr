import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { addMagazine, setMagazineAddDefault } from 'Store/Actions/searchActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createSystemStatusSelector from 'Store/Selectors/createSystemStatusSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import monitorOptions from 'Utilities/Magazine/monitorOptions';
import AddNewMagazineModalContent from './AddNewMagazineModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.search,
    (state) => state.settings.metadataProfiles,
    (state) => state.settings.qualityProfiles.items,
    (state) => state.magazineRootFolders.items,
    createDimensionsSelector(),
    createSystemStatusSelector(),
    (searchState, metadataProfiles, qualityProfileItems, rootFolderItems, dimensions, systemStatus) => {
      const {
        isAdding,
        addError,
        magazineDefaults
      } = searchState;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(magazineDefaults, {}, addError);

      const fallbackRootFolder = rootFolderItems[0] || null;
      const rootFolderValues = rootFolderItems.length ? rootFolderItems.map((item) => {
        return {
          key: item.path,
          value: item.name ? `${item.name} (${item.path})` : item.path
        };
      }) : [{
        key: '',
        value: 'No magazine root folders configured',
        isDisabled: true
      }];

      const fallbackRootFolderPath = fallbackRootFolder ? fallbackRootFolder.path : '';
      const fallbackQualityProfileId =
        (fallbackRootFolder && fallbackRootFolder.defaultQualityProfileId) ||
        (qualityProfileItems[0] ? qualityProfileItems[0].id : 0);
      const fallbackMetadataProfileId =
        (fallbackRootFolder && fallbackRootFolder.defaultMetadataProfileId) ||
        (metadataProfiles.items[0] ? metadataProfiles.items[0].id : 0);

      const selectedQualityProfileId = parseInt(settings.qualityProfileId.value);
      const selectedMetadataProfileId = parseInt(settings.metadataProfileId.value);
      const selectedRootFolderPath = settings.rootFolderPath.value || '';
      const selectedRootFolder = rootFolderItems.find((item) => item.path === selectedRootFolderPath) || fallbackRootFolder;
      const fallbackMonitor = (selectedRootFolder && selectedRootFolder.defaultMonitorOption) || monitorOptions[0].key;

      const normalizedRootFolderPath = selectedRootFolderPath || fallbackRootFolderPath;
      const normalizedQualityProfileId = (!selectedQualityProfileId || selectedQualityProfileId < 0) ?
        ((selectedRootFolder && selectedRootFolder.defaultQualityProfileId) || fallbackQualityProfileId) :
        selectedQualityProfileId;
      const normalizedMetadataProfileId = (!selectedMetadataProfileId || selectedMetadataProfileId < 0) ?
        ((selectedRootFolder && selectedRootFolder.defaultMetadataProfileId) || fallbackMetadataProfileId) :
        selectedMetadataProfileId;
      const normalizedMonitor = settings.monitor.value || fallbackMonitor;

      return {
        isAdding,
        addError,
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        isWindows: systemStatus.isWindows,
        isAddDisabled: !normalizedRootFolderPath || !normalizedQualityProfileId || !normalizedMetadataProfileId,
        fallbackRootFolderPath,
        fallbackQualityProfileId,
        fallbackMetadataProfileId,
        rootFolders: rootFolderItems,
        ...settings,
        rootFolderValues,
        rootFolderPath: {
          ...settings.rootFolderPath,
          value: normalizedRootFolderPath
        },
        qualityProfileId: {
          ...settings.qualityProfileId,
          value: normalizedQualityProfileId
        },
        metadataProfileId: {
          ...settings.metadataProfileId,
          value: normalizedMetadataProfileId
        },
        monitor: {
          ...settings.monitor,
          value: normalizedMonitor
        }
      };
    }
  );
}

const mapDispatchToProps = {
  setMagazineAddDefault,
  addMagazine
};

class AddNewMagazineModalContentConnector extends Component {

  onInputChange = ({ name, value }) => {
    this.props.setMagazineAddDefault({ [name]: value });
  };

  onAddMagazinePress = (searchForMissingIssues) => {
    const {
      foreignId,
      rootFolderPath,
      monitor,
      qualityProfileId,
      metadataProfileId,
      tags,
      rootFolders,
      fallbackRootFolderPath,
      fallbackQualityProfileId,
      fallbackMetadataProfileId
    } = this.props;

    const selectedRootFolderPath = rootFolderPath ? rootFolderPath.value : '';
    const selectedQualityProfileId = parseInt(qualityProfileId.value);
    const selectedMetadataProfileId = parseInt(metadataProfileId.value);
    const selectedRootFolder = rootFolders.find((item) => item.path === selectedRootFolderPath);

    const normalizedRootFolderPath = selectedRootFolderPath || fallbackRootFolderPath;
    const normalizedQualityProfileId = (!selectedQualityProfileId || selectedQualityProfileId < 0) ?
      ((selectedRootFolder && selectedRootFolder.defaultQualityProfileId) || fallbackQualityProfileId) :
      selectedQualityProfileId;
    const normalizedMetadataProfileId = (!selectedMetadataProfileId || selectedMetadataProfileId < 0) ?
      ((selectedRootFolder && selectedRootFolder.defaultMetadataProfileId) || fallbackMetadataProfileId) :
      selectedMetadataProfileId;

    if (!normalizedRootFolderPath || !normalizedQualityProfileId || !normalizedMetadataProfileId) {
      return;
    }

    this.props.addMagazine({
      foreignId,
      rootFolderPath: normalizedRootFolderPath,
      monitor: monitor.value,
      qualityProfileId: normalizedQualityProfileId,
      metadataProfileId: normalizedMetadataProfileId,
      tags: tags.value,
      searchForMissingIssues
    });
  };

  render() {
    return (
      <AddNewMagazineModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onAddMagazinePress={this.onAddMagazinePress}
      />
    );
  }
}

AddNewMagazineModalContentConnector.propTypes = {
  foreignId: PropTypes.string.isRequired,
  rootFolderPath: PropTypes.object,
  rootFolderValues: PropTypes.arrayOf(PropTypes.object).isRequired,
  rootFolders: PropTypes.arrayOf(PropTypes.object).isRequired,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  tags: PropTypes.object.isRequired,
  fallbackRootFolderPath: PropTypes.string,
  fallbackQualityProfileId: PropTypes.number,
  fallbackMetadataProfileId: PropTypes.number,
  isAddDisabled: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  setMagazineAddDefault: PropTypes.func.isRequired,
  addMagazine: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewMagazineModalContentConnector);
