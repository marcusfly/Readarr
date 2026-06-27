import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import SelectInput from 'Components/Form/SelectInput';
import { inputTypes } from 'Helpers/Props';
import magazineMonitorOptions from 'Utilities/Magazine/monitorOptions';
import translate from 'Utilities/String/translate';
import styles from './AddMagazineOptionsForm.css';

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
    const rootFolderValue = rootFolderPath ? rootFolderPath.value : '';
    const rootFolderErrors = rootFolderPath ? rootFolderPath.errors : [];
    const rootFolderWarnings = rootFolderPath ? rootFolderPath.warnings : [];

    return (
      <Form {...otherProps}>
        <FormGroup>
          <FormLabel>
            {translate('RootFolder')}
          </FormLabel>

          <div className={styles.rootFolderField}>
            <SelectInput
              name="rootFolderPath"
              value={rootFolderValue}
              values={rootFolderValues}
              isDisabled={!rootFolderValues.length || rootFolderValues.every((option) => option.isDisabled)}
              onChange={onInputChange}
              hasError={!!rootFolderErrors.length}
              hasWarning={!rootFolderErrors.length && !!rootFolderWarnings.length}
            />

            <FormInputHelpText
              className={styles.rootFolderHelpText}
              text="Choose the root folder where the magazine should be stored."
            />
          </div>
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
