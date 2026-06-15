import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import AddNewMagazineModal from './AddNewMagazineModal';
import styles from '../Author/AddNewAuthorSearchResult.css';

class AddNewMagazineSearchResult extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isNewAddMagazineModalOpen: false
    };
  }

  componentDidUpdate(prevProps) {
    if (!prevProps.isExistingMagazine && this.props.isExistingMagazine) {
      this.onAddMagazineModalClose();
    }
  }

  onPress = () => {
    this.setState({ isNewAddMagazineModalOpen: true });
  };

  onAddMagazineModalClose = () => {
    this.setState({ isNewAddMagazineModalOpen: false });
  };

  onExternalLinkPress = (event) => {
    event.stopPropagation();
  };

  render() {
    const {
      foreignId,
      title,
      cleanTitle,
      issn,
      publisher,
      wikidataId,
      isExistingMagazine,
      isSmallScreen
    } = this.props;

    const {
      isNewAddMagazineModalOpen
    } = this.state;

    const externalUrl = wikidataId ?
      `https://www.wikidata.org/wiki/${wikidataId}` :
      `https://www.wikidata.org/wiki/Special:Search?search=${encodeURIComponent(title)}`;

    const linkProps = isExistingMagazine ? { to: externalUrl } : { onPress: this.onPress };

    return (
      <div className={styles.searchResult}>
        <Link
          className={styles.underlay}
          {...linkProps}
        />

        <div className={styles.overlay}>
          <div className={styles.content}>
            <div className={styles.nameRow}>
              <div className={styles.nameContainer}>
                <div className={styles.name}>
                  {title}
                </div>
              </div>

              <div className={styles.icons}>
                {
                  isExistingMagazine ?
                    <Icon
                      className={styles.alreadyExistsIcon}
                      name={icons.CHECK_CIRCLE}
                      size={36}
                      title={translate('AlreadyInYourLibrary')}
                    /> :
                    null
                }

                <Link
                  className={styles.mbLink}
                  to={externalUrl}
                  onPress={this.onExternalLinkPress}
                >
                  <Icon
                    className={styles.mbLinkIcon}
                    name={icons.EXTERNAL_LINK}
                    size={28}
                  />
                </Link>
              </div>
            </div>

            {
              cleanTitle && cleanTitle !== title ?
                <div className={styles.overview}>
                  {cleanTitle}
                </div> :
                null
            }

            {
              publisher ?
                <div className={styles.overview}>
                  Publisher: {publisher}
                </div> :
                null
            }

            {
              issn ?
                <div className={styles.overview}>
                  ISSN: {issn}
                </div> :
                null
            }
          </div>
        </div>

        <AddNewMagazineModal
          isOpen={isNewAddMagazineModalOpen && !isExistingMagazine}
          foreignId={foreignId}
          title={title}
          cleanTitle={cleanTitle}
          issn={issn}
          publisher={publisher}
          isSmallScreen={isSmallScreen}
          onModalClose={this.onAddMagazineModalClose}
        />
      </div>
    );
  }
}

AddNewMagazineSearchResult.propTypes = {
  foreignId: PropTypes.string.isRequired,
  title: PropTypes.string.isRequired,
  cleanTitle: PropTypes.string,
  issn: PropTypes.string,
  publisher: PropTypes.string,
  wikidataId: PropTypes.string,
  isExistingMagazine: PropTypes.bool.isRequired,
  isSmallScreen: PropTypes.bool.isRequired
};

export default AddNewMagazineSearchResult;
