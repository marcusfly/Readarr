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
  render() {
    const {
      rootFolderPath,
      rootFolderValues,
      monitor,
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
  tags: PropTypes.object.isRequired,
  onInputChange: PropTypes.func.isRequired
};

export default AddMagazineOptionsForm;
