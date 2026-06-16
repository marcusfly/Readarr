import PropTypes from 'prop-types';
import React from 'react';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';
import monitorOptions from 'Utilities/Magazine/monitorOptions';
import translate from 'Utilities/String/translate';
import styles from '../RootFolder/EditRootFolderModalContent.css';

function EditMagazineRootFolderModalContent(props) {
  const {
    isFetching,
    error,
    isSaving,
    saveError,
    item,
    onInputChange,
    onModalClose,
    onSavePress,
    onDeleteRootFolderPress,
    ...otherProps
  } = props;

  const {
    id,
    name,
    path,
    defaultMonitorOption
  } = item;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {id ? 'Edit Magazine Root Folder' : 'Add Magazine Root Folder'}
      </ModalHeader>

      <ModalBody>
        {
          isFetching &&
            <LoadingIndicator />
        }

        {
          !isFetching && !!error &&
            <div>
              Unable to load the magazine root folder. Please try again.
            </div>
        }

        {
          !isFetching && !error &&
            <Form {...otherProps}>
              <FieldSet legend="Magazine Root Folder">
                <FormGroup>
                  <FormLabel>
                    {translate('Name')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.TEXT}
                    name="name"
                    {...name}
                    onChange={onInputChange}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>
                    {translate('Path')}
                  </FormLabel>

                  <FormInputGroup
                    type={id ? inputTypes.TEXT : inputTypes.PATH}
                    readOnly={!!id}
                    name="path"
                    helpText="Root folder containing your magazine library"
                    helpTextWarning="This must be different to the directory where your download client puts files"
                    {...path}
                    onChange={onInputChange}
                  />
                </FormGroup>
              </FieldSet>

              <FieldSet legend="Added Magazine Settings">
                <FormGroup>
                  <FormLabel>
                    {translate('Monitor')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.SELECT}
                    name="defaultMonitorOption"
                    values={monitorOptions}
                    {...defaultMonitorOption}
                    onChange={onInputChange}
                  />
                </FormGroup>
              </FieldSet>
            </Form>
        }
      </ModalBody>

      <ModalFooter>
        {
          id &&
            <Button
              className={styles.deleteButton}
              kind={kinds.DANGER}
              onPress={onDeleteRootFolderPress}
            >
              {translate('Delete')}
            </Button>
        }

        <Button onPress={onModalClose}>
          {translate('Cancel')}
        </Button>

        <SpinnerErrorButton
          isSpinning={isSaving}
          error={saveError}
          onPress={onSavePress}
        >
          {translate('Save')}
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

EditMagazineRootFolderModalContent.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  item: PropTypes.object.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onDeleteRootFolderPress: PropTypes.func
};

EditMagazineRootFolderModalContent.defaultProps = {
  error: null,
  saveError: null,
  onDeleteRootFolderPress: null
};

export default EditMagazineRootFolderModalContent;
