import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import CheckInput from 'Components/Form/CheckInput';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import AddMagazineOptionsForm from './AddMagazineOptionsForm';
import styles from '../Author/AddNewAuthorModalContent.css';

class AddNewMagazineModalContent extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      searchForMissingIssues: false
    };
  }

  onSearchForMissingIssuesChange = ({ value }) => {
    this.setState({ searchForMissingIssues: value });
  };

  onAddMagazinePress = () => {
    this.props.onAddMagazinePress(this.state.searchForMissingIssues);
  };

  render() {
    const {
      title,
      cleanTitle,
      issn,
      publisher,
      rootFolderPath,
      fallbackRootFolderPath,
      isAdding,
      isSmallScreen,
      onModalClose,
      isAddDisabled,
      ...otherProps
    } = this.props;

    const hasRootFolder = !!((rootFolderPath && rootFolderPath.value) || fallbackRootFolderPath);

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Add New Magazine
        </ModalHeader>

        <ModalBody>
          <div className={styles.container}>
            <div className={styles.info}>
              <div className={styles.name}>
                {title}
              </div>

              {
                cleanTitle && cleanTitle !== title &&
                  <div className={styles.disambiguation}>
                    {cleanTitle}
                  </div>
              }

              {
                publisher &&
                  <div className={styles.overview}>
                    Publisher: {publisher}
                  </div>
              }

              {
                issn &&
                  <div className={styles.overview}>
                    ISSN: {issn}
                  </div>
              }

              <AddMagazineOptionsForm
                folder={cleanTitle || title}
                {...otherProps}
              />
            </div>
          </div>
        </ModalBody>

        <ModalFooter className={styles.modalFooter}>
          {
            !hasRootFolder &&
              <Alert kind={kinds.WARNING}>
                Configure a root folder before adding magazines.
              </Alert>
          }

          <label className={styles.searchForMissingBooksLabelContainer}>
            <span className={styles.searchForMissingBooksLabel}>
              Start search for missing issues
            </span>

            <CheckInput
              containerClassName={styles.searchForMissingBooksContainer}
              className={styles.searchForMissingBooksInput}
              name="searchForMissingIssues"
              value={this.state.searchForMissingIssues}
              onChange={this.onSearchForMissingIssuesChange}
            />
          </label>

          <SpinnerButton
            className={styles.addButton}
            kind={kinds.SUCCESS}
            isSpinning={isAdding}
            isDisabled={isAddDisabled || !hasRootFolder}
            onPress={this.onAddMagazinePress}
          >
            Add {title}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    );
  }
}

AddNewMagazineModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  cleanTitle: PropTypes.string,
  issn: PropTypes.string,
  publisher: PropTypes.string,
  rootFolderPath: PropTypes.object,
  rootFolderValues: PropTypes.arrayOf(PropTypes.object),
  fallbackRootFolderPath: PropTypes.string,
  isAdding: PropTypes.bool.isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  isAddDisabled: PropTypes.bool.isRequired,
  onAddMagazinePress: PropTypes.func.isRequired
};

export default AddNewMagazineModalContent;
