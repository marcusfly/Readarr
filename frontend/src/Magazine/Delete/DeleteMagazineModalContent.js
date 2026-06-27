import PropTypes from 'prop-types';
import React, { Component } from 'react';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';

class DeleteMagazineModalContent extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      deleteFiles: false
    };
  }

  onDeleteFilesChange = ({ value }) => {
    this.setState({ deleteFiles: value });
  };

  onDeleteMagazineConfirmed = () => {
    this.props.onDeletePress(this.state.deleteFiles);
    this.setState({ deleteFiles: false });
  };

  render() {
    const {
      title,
      statistics = {},
      onModalClose
    } = this.props;

    const {
      issueFileCount = 0
    } = statistics;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Delete - {title}
        </ModalHeader>

        <ModalBody>
          <FormGroup>
            <FormLabel>
              Delete {issueFileCount} Imported Issue Files
            </FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="deleteFiles"
              value={this.state.deleteFiles}
              helpText="Delete imported magazine issue files from disk."
              kind={kinds.DANGER}
              onChange={this.onDeleteFilesChange}
            />
          </FormGroup>
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>
            Close
          </Button>

          <Button
            kind={kinds.DANGER}
            onPress={this.onDeleteMagazineConfirmed}
          >
            Delete
          </Button>
        </ModalFooter>
      </ModalContent>
    );
  }
}

DeleteMagazineModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  statistics: PropTypes.object,
  onDeletePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default DeleteMagazineModalContent;
