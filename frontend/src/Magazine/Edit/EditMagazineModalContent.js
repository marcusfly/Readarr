import PropTypes from 'prop-types';
import React from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes } from 'Helpers/Props';
import monitorOptions from 'Utilities/Magazine/monitorOptions';
import translate from 'Utilities/String/translate';

function EditMagazineModalContent({
  title,
  publisher,
  issn,
  path,
  monitor,
  searchForMissingIssues,
  item,
  isSaving,
  onInputChange,
  onSavePress,
  onModalClose,
  ...otherProps
}) {
  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        Edit - {title}
      </ModalHeader>

      <ModalBody>
        <Form {...otherProps}>
          <FormGroup>
            <FormLabel>
              {translate('Monitored')}
            </FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="monitored"
              helpText="Track this magazine for issue searches, monitoring, and import processing."
              {...item.monitored}
              onChange={onInputChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>
              Issue Monitoring
            </FormLabel>

            <FormInputGroup
              type={inputTypes.SELECT}
              name="monitor"
              values={monitorOptions}
              helpText="Choose which issues should stay monitored for this magazine."
              {...monitor}
              onChange={onInputChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>
              Search For Missing Issues
            </FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="searchForMissingIssues"
              helpText="Keep automatic missing-issue searches enabled for this magazine."
              {...searchForMissingIssues}
              onChange={onInputChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>
              {translate('Tags')}
            </FormLabel>

            <FormInputGroup
              type={inputTypes.TAG}
              name="tags"
              {...item.tags}
              onChange={onInputChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>
              Publisher
            </FormLabel>

            <FormInputGroup
              value={publisher || 'Unknown publisher'}
              isDisabled={true}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>
              ISSN
            </FormLabel>

            <FormInputGroup
              value={issn || 'Not available'}
              isDisabled={true}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>
              {translate('Path')}
            </FormLabel>

            <FormInputGroup
              value={path || ''}
              isDisabled={true}
            />
          </FormGroup>
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>
          Cancel
        </Button>

        <SpinnerButton
          isSpinning={isSaving}
          onPress={onSavePress}
        >
          Save
        </SpinnerButton>
      </ModalFooter>
    </ModalContent>
  );
}

EditMagazineModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  publisher: PropTypes.string,
  issn: PropTypes.string,
  path: PropTypes.string,
  monitor: PropTypes.object.isRequired,
  searchForMissingIssues: PropTypes.object.isRequired,
  item: PropTypes.object.isRequired,
  isSaving: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

EditMagazineModalContent.defaultProps = {
  publisher: '',
  issn: '',
  path: ''
};

export default EditMagazineModalContent;
