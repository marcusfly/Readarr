import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import {
  saveMagazineRootFolder,
  setMagazineRootFolderValue
} from 'Store/Actions/magazineRootFolderActions';
import selectSettings from 'Store/Selectors/selectSettings';
import EditMagazineRootFolderModalContent from './EditMagazineRootFolderModalContent';

function createMapStateToProps() {
  return createSelector(
    (state, { id }) => id,
    (state) => state.magazineRootFolders,
    (id, section) => {
      const source = id ? _.find(section.items, { id }) : Object.assign({ name: '' }, section.schema);
      const settings = selectSettings(source, section.pendingChanges, section.saveError);

      return {
        isFetching: section.isFetching,
        isSaving: section.isSaving,
        saveError: section.saveError,
        error: section.error,
        ...settings,
        item: settings.settings
      };
    }
  );
}

const mapDispatchToProps = {
  setMagazineRootFolderValue,
  saveMagazineRootFolder
};

class EditMagazineRootFolderModalContentConnector extends Component {
  componentDidUpdate(prevProps) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }
  }

  onInputChange = ({ name, value }) => {
    this.props.setMagazineRootFolderValue({ name, value });
  };

  onSavePress = () => {
    this.props.saveMagazineRootFolder({ id: this.props.id });
  };

  render() {
    return (
      <EditMagazineRootFolderModalContent
        {...this.props}
        onSavePress={this.onSavePress}
        onInputChange={this.onInputChange}
      />
    );
  }
}

EditMagazineRootFolderModalContentConnector.propTypes = {
  id: PropTypes.number,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  item: PropTypes.object.isRequired,
  setMagazineRootFolderValue: PropTypes.func.isRequired,
  saveMagazineRootFolder: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditMagazineRootFolderModalContentConnector);
