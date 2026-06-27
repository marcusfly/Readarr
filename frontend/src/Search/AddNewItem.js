import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { icons, kinds } from 'Helpers/Props';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import AddNewAuthorSearchResultConnector from './Author/AddNewAuthorSearchResultConnector';
import AddNewBookSearchResultConnector from './Book/AddNewBookSearchResultConnector';
import AddNewMagazineSearchResultConnector from './Magazine/AddNewMagazineSearchResultConnector';
import { searchScopeOptions, searchScopes } from './searchScopes';
import styles from './AddNewItem.css';

const MIN_LOADING_INDICATOR_MS = 350;

class AddNewItem extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._loadingIndicatorTimeout = null;
    this._fetchStartedAt = null;

    this.state = {
      term: props.term || '',
      searchScope: props.initialSearchScope,
      isFetching: false,
      hasSearched: false
    };
  }

  componentDidMount() {
    const term = this.state.term;

    if (term) {
      this.setState({ hasSearched: true, isFetching: true }, () => {
        this._fetchStartedAt = Date.now();
        this.props.onSearchChange(term, this.state.searchScope);
      });
    }
  }

  componentDidUpdate(prevProps) {
    const {
      term,
      initialSearchScope,
      isFetching
    } = this.props;

    if (term && (term !== prevProps.term || initialSearchScope !== prevProps.initialSearchScope)) {
      this.setState({
        term,
        searchScope: initialSearchScope,
        isFetching: true,
        hasSearched: true
      }, () => {
        this._fetchStartedAt = Date.now();
        this.props.onSearchChange(term, initialSearchScope);
      });
    } else if (initialSearchScope !== prevProps.initialSearchScope) {
      this.setState({
        searchScope: initialSearchScope
      });
    } else if (isFetching !== prevProps.isFetching) {
      if (isFetching) {
        this._clearLoadingIndicatorTimeout();
        this._fetchStartedAt = Date.now();

        this.setState({
          isFetching: true
        });
      } else {
        const elapsed = this._fetchStartedAt ? Date.now() - this._fetchStartedAt : MIN_LOADING_INDICATOR_MS;
        const remaining = Math.max(MIN_LOADING_INDICATOR_MS - elapsed, 0);

        if (remaining > 0) {
          this._loadingIndicatorTimeout = setTimeout(() => {
            this._loadingIndicatorTimeout = null;
            this._fetchStartedAt = null;

            this.setState({
              isFetching: false
            });
          }, remaining);
        } else {
          this._fetchStartedAt = null;

          this.setState({
            isFetching: false
          });
        }
      }
    }
  }

  componentWillUnmount() {
    this._clearLoadingIndicatorTimeout();
  }

  _clearLoadingIndicatorTimeout = () => {
    if (this._loadingIndicatorTimeout) {
      clearTimeout(this._loadingIndicatorTimeout);
      this._loadingIndicatorTimeout = null;
    }
  };

  onSearch = (term) => {
    const trimmedTerm = term.trim();

    if (!trimmedTerm) {
      this.props.onClearSearch();
      this.setState({ hasSearched: false });
      return;
    }

    this.setState({ isFetching: true, hasSearched: true }, () => {
      this._clearLoadingIndicatorTimeout();
      this._fetchStartedAt = Date.now();
      this.props.onSearchChange(trimmedTerm, this.state.searchScope);
    });
  };

  //
  // Listeners

  onSearchInputChange = ({ value }) => {
    const hasValue = !!value.trim();
    const { searchWhileTyping } = this.props;

    this.setState({
      term: value,
      isFetching: false,
      hasSearched: false
    }, () => {
      if (!hasValue) {
        this.props.onClearSearch();
        return;
      }

      if (searchWhileTyping) {
        this.onSearch(value);
      }
    });
  };

  onSearchInputKeyDown = (event) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.onSearchSubmit();
    }
  };

  onSearchSubmit = () => {
    const term = this.state.term;
    this.onSearch(term);
  };

  onSearchScopeChange = (event) => {
    const searchScope = event.target.value;
    const term = this.state.term.trim();

    this.setState({
      searchScope
    }, () => {
      if (term) {
        this.onSearch(term);
      } else {
        this.props.onClearSearch();
      }
    });
  };

  onClearSearchPress = () => {
    this.setState({
      term: '',
      hasSearched: false
    });

    this.props.onClearSearch();
  };

  //
  // Render

  render() {
    const {
      error,
      items,
      hasExistingAuthors
    } = this.props;

    const term = this.state.term;
    const searchScope = this.state.searchScope;
    const isFetching = this.state.isFetching;
    const hasSearched = this.state.hasSearched;
    const isComicsScope = searchScope === searchScopes.COMICS;

    return (
      <PageContent title={translate('AddNewItem')}>
        <PageContentBody>
          <div className={styles.searchContainer}>
            <select
              className={styles.searchScopeSelect}
              value={searchScope}
              onChange={this.onSearchScopeChange}
            >
              {
                searchScopeOptions.map((option) => (
                  <option
                    key={option.key}
                    value={option.key}
                  >
                    {option.value}
                  </option>
                ))
              }
            </select>

            <div className={styles.searchIconContainer}>
              {
                isFetching ?
                  <LoadingIndicator
                    className={styles.searchLoadingIndicator}
                    rippleClassName={styles.searchLoadingRipple}
                    size={20}
                  /> :
                  <Icon
                    name={icons.SEARCH}
                    size={20}
                  />
              }
            </div>

            <TextInput
              className={styles.searchInput}
              name="searchBox"
              value={term}
              placeholder={translate('SearchBoxPlaceHolder')}
              autoFocus={true}
              onChange={this.onSearchInputChange}
              onKeyDown={this.onSearchInputKeyDown}
            />

            <Button
              className={styles.clearLookupButton}
              onPress={this.onClearSearchPress}
            >
              <Icon
                name={icons.REMOVE}
                size={20}
              />
            </Button>
          </div>

          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            !isFetching && !!error ?
              <div className={styles.message}>
                <div className={styles.helpText}>
                  {translate('FailedLoadingSearchResults')}
                </div>

                <Alert kind={kinds.WARNING}>{getErrorMessage(error)}</Alert>

                <div>
                  <Link to="https://wiki.servarr.com/readarr/troubleshooting#invalid-response-received-from-metadata-api">
                    {translate('WhySearchesCouldBeFailing')}
                  </Link>
                </div>
              </div> : null
          }

          {
            !isFetching && !error && !!items.length &&
              <div className={styles.searchResults}>
                {
                  items.map((item) => {
                    if (item.author) {
                      const author = item.author;
                      return (
                        <AddNewAuthorSearchResultConnector
                          key={item.id}
                          {...author}
                        />
                      );
                    } else if (item.magazine) {
                      const magazine = item.magazine;
                      return (
                        <AddNewMagazineSearchResultConnector
                          key={item.id}
                          searchResultId={item.id}
                          isExistingMagazine={'id' in magazine && magazine.id !== 0}
                          {...magazine}
                        />
                      );
                    } else if (item.book) {
                      const book = item.book;
                      return (
                        <AddNewBookSearchResultConnector
                          key={item.id}
                          isExistingBook={'id' in book && book.id !== 0}
                          isExistingAuthor={'id' in book.author && book.author.id !== 0}
                          {...book}
                        />
                      );
                    }
                    return null;
                  })
                }
              </div>
          }

          {
            !isFetching && !error && hasSearched && !!term && !items.length &&
              <div className={styles.message}>
                <div className={styles.noResults}>
                  {translate('CouldntFindAnyResultsForTerm', [term])}
                </div>
                {
                  isComicsScope ?
                    <div>
                      Comic metadata search is not available yet.
                    </div> :
                    <div>
                      You can also search using an Open Library ID for an author (e.g. author:OL23919A), work (e.g. work:OL45883W), or edition (e.g. edition:OL7353617M), or search by ISBN (e.g. isbn:9780439554930)
                    </div>
                }
              </div>
          }

          {
            term ?
              null :
              <div className={styles.message}>
                <div className={styles.helpText}>
                  It's easy to add a new author, book, or magazine. Just start typing the name of the item you want to add.
                </div>
                {
                  isComicsScope ?
                    <div>
                      Comic metadata search is reserved for the future comics pipeline and is not available yet.
                    </div> :
                    <div>
                      You can also search using an Open Library ID for an author (e.g. author:OL23919A), work (e.g. work:OL45883W), or edition (e.g. edition:OL7353617M), search by ISBN (e.g. isbn:9780439554930), or search by Wikidata id (e.g. Q123456).
                    </div>
                }
              </div>
          }

          {
            !term && !hasExistingAuthors ?
              <div className={styles.message}>
                <div className={styles.noAuthorsText}>
                  You haven't added any authors yet, do you want to add an existing library location (Root Folder) and update?
                </div>
                <div>
                  <Button
                    to="/settings/mediamanagement"
                    kind={kinds.PRIMARY}
                  >
                    {translate('AddRootFolder')}
                  </Button>
                </div>
              </div> :
              null
          }

          <div />
        </PageContentBody>
      </PageContent>
    );
  }
}

AddNewItem.propTypes = {
  term: PropTypes.string,
  initialSearchScope: PropTypes.string.isRequired,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  hasExistingAuthors: PropTypes.bool.isRequired,
  searchWhileTyping: PropTypes.bool,
  onSearchChange: PropTypes.func.isRequired,
  onClearSearch: PropTypes.func.isRequired
};

export default AddNewItem;
