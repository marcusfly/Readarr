import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { updateItem } from 'Store/Actions/baseActions';
import { fetchMagazineRootFolders } from 'Store/Actions/magazineRootFolderActions';
import { setMagazineAddDefault } from 'Store/Actions/searchActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createSystemStatusSelector from 'Store/Selectors/createSystemStatusSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getNewMagazine from 'Utilities/Magazine/getNewMagazine';
import monitorOptions from 'Utilities/Magazine/monitorOptions';
import AddNewMagazineModalContent from './AddNewMagazineModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.search,
    (state) => state.magazineRootFolders.items,
    (state, ownProps) => ownProps.searchResultId,
    (state, ownProps) => ownProps.foreignId,
    createDimensionsSelector(),
    createSystemStatusSelector(),
    (searchState, rootFolderItems, searchResultId, foreignId, dimensions, systemStatus) => {
      const {
        isAdding,
        addError,
        magazineDefaults,
        items
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
      const selectedRootFolderPath = settings.rootFolderPath.value || '';
      const selectedRootFolder = rootFolderItems.find((item) => item.path === selectedRootFolderPath) || fallbackRootFolder;
      const fallbackMonitor = (selectedRootFolder && selectedRootFolder.defaultMonitorOption) || monitorOptions[0].key;

      const normalizedRootFolderPath = selectedRootFolderPath || fallbackRootFolderPath;
      const normalizedMonitor = settings.monitor.value || fallbackMonitor;
      const sourceMagazine = items.find((item) => item.id === searchResultId)?.magazine || null;

      return {
        isAdding,
        addError,
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        isWindows: systemStatus.isWindows,
        isAddDisabled: !normalizedRootFolderPath,
        fallbackRootFolderPath,
        rootFolders: rootFolderItems,
        sourceMagazine,
        ...settings,
        rootFolderValues,
        rootFolderPath: {
          ...settings.rootFolderPath,
          value: normalizedRootFolderPath
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
  fetchMagazineRootFolders,
  setMagazineAddDefault,
  updateItem
};

class AddNewMagazineModalContentConnector extends Component {
  state = {
    isSubmitting: false
  };

  componentDidMount() {
    const {
      fetchMagazineRootFolders: dispatchFetchMagazineRootFolders,
      rootFolders,
      rootFolderPath
    } = this.props;

    if (!rootFolders.length) {
      dispatchFetchMagazineRootFolders();
    }

    this.ensureRootFolderDefault(rootFolderPath);
  }

  componentDidUpdate(prevProps) {
    const {
      rootFolders,
      rootFolderPath
    } = this.props;

    if (prevProps.rootFolders !== rootFolders || prevProps.rootFolderPath?.value !== rootFolderPath?.value) {
      this.ensureRootFolderDefault(rootFolderPath);
    }
  }

  ensureRootFolderDefault = (rootFolderPath = this.props.rootFolderPath) => {
    const {
      rootFolders,
      setMagazineAddDefault: updateMagazineAddDefault
    } = this.props;

    if (!rootFolders.length) {
      return;
    }

    const selectedRootFolderPath = rootFolderPath ? rootFolderPath.value : '';

    if (selectedRootFolderPath && rootFolders.some((rootFolder) => rootFolder.path === selectedRootFolderPath)) {
      return;
    }

    updateMagazineAddDefault({ rootFolderPath: rootFolders[0].path });
  };

  onInputChange = ({ name, value }) => {
    this.props.setMagazineAddDefault({ [name]: value });
  };

  onAddMagazinePress = (searchForMissingIssues) => {
    const {
      searchResultId,
      foreignId,
      sourceMagazine,
      rootFolderPath,
      monitor,
      tags,
      fallbackRootFolderPath,
      onModalClose
    } = this.props;

    const selectedRootFolderPath = rootFolderPath ? rootFolderPath.value : '';
    const normalizedRootFolderPath = selectedRootFolderPath || fallbackRootFolderPath;

    if (!normalizedRootFolderPath || !sourceMagazine) {
      return;
    }

    const payload = {
      foreignId,
      rootFolderPath: normalizedRootFolderPath,
      monitor: monitor.value,
      qualityProfileId: 0,
      metadataProfileId: 0,
      tags: tags.value,
      searchForMissingIssues
    };

    const newMagazine = getNewMagazine(_.cloneDeep(sourceMagazine), payload);

    this.setState({ isSubmitting: true });

    createAjaxRequest({
      url: '/magazine',
      method: 'POST',
      dataType: 'json',
      contentType: 'application/json',
      data: JSON.stringify(newMagazine)
    }).request.done((data) => {
      this.props.updateItem({
        section: 'search',
        id: searchResultId,
        foreignId,
        magazine: data
      });

      this.setState({ isSubmitting: false }, onModalClose);
    }).fail(() => {
      this.setState({ isSubmitting: false });
    });
  };

  render() {
    return (
      <AddNewMagazineModalContent
        {...this.props}
        isAdding={this.state.isSubmitting}
        onInputChange={this.onInputChange}
        onAddMagazinePress={this.onAddMagazinePress}
      />
    );
  }
}

AddNewMagazineModalContentConnector.propTypes = {
  searchResultId: PropTypes.number.isRequired,
  foreignId: PropTypes.string.isRequired,
  sourceMagazine: PropTypes.object,
  rootFolderPath: PropTypes.object,
  rootFolderValues: PropTypes.arrayOf(PropTypes.object).isRequired,
  rootFolders: PropTypes.arrayOf(PropTypes.object).isRequired,
  monitor: PropTypes.object.isRequired,
  tags: PropTypes.object.isRequired,
  fallbackRootFolderPath: PropTypes.string,
  isAddDisabled: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  fetchMagazineRootFolders: PropTypes.func.isRequired,
  setMagazineAddDefault: PropTypes.func.isRequired,
  updateItem: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewMagazineModalContentConnector);
