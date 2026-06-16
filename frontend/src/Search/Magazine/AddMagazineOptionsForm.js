import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import { inputTypes } from 'Helpers/Props';
import magazineMonitorOptions from 'Utilities/Magazine/monitorOptions';
import translate from 'Utilities/String/translate';

class AddMagazineOptionsForm extends Component {

  onQualityProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'qualityProfileId', value: parseInt(value) });
  };

  onMetadataProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'metadataProfileId', value: parseInt(value) });
  };

  render() {
    const {
      rootFolderPath,
      rootFolderValues,
      monitor,
      qualityProfileId,
      metadataProfileId,
      tags,
      onInputChange,
      ...otherProps
    } = this.props;

    return (
      <Form {...otherProps}>
        <FormGroup>
          <FormLabel>
            {translate('RootFolder')}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.SELECT}
            name="rootFolderPath"
            values={rootFolderValues}
            helpText="Choose the root folder where the magazine should be stored."
            onChange={onInputChange}
            {...rootFolderPath}
          />
        </FormGroup>

        <FormGroup>
          <FormLabel>
            Monitor
          </FormLabel>

          <FormInputGroup
            type={inputTypes.SELECT}
            name="monitor"
            values={magazineMonitorOptions}
            helpText="Choose which magazine issues should be monitored."
            onChange={onInputChange}
            {...monitor}
          />
        </FormGroup>

        <FormGroup>
          <FormLabel>
            {translate('QualityProfile')}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.QUALITY_PROFILE_SELECT}
            name="qualityProfileId"
            onChange={this.onQualityProfileIdChange}
            {...qualityProfileId}
          />
        </FormGroup>

        <FormGroup>
          <FormLabel>
            {translate('MetadataProfile')}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.METADATA_PROFILE_SELECT}
            name="metadataProfileId"
            includeNone={false}
            onChange={this.onMetadataProfileIdChange}
            {...metadataProfileId}
          />
        </FormGroup>

        <FormGroup>
          <FormLabel>
            {translate('Tags')}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.TAG}
            name="tags"
            onChange={onInputChange}
            {...tags}
          />
        </FormGroup>
      </Form>
    );
  }
}

AddMagazineOptionsForm.propTypes = {
  rootFolderPath: PropTypes.object,
  rootFolderValues: PropTypes.arrayOf(PropTypes.object).isRequired,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  tags: PropTypes.object.isRequired,
  onInputChange: PropTypes.func.isRequired
};

export default AddMagazineOptionsForm;
