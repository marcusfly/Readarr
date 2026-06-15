import PropTypes from 'prop-types';
import React from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { inputTypes, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

const metadataCapability = {
  authorLookup: 1 << 0,
  bookLookup: 1 << 1,
  authorSearch: 1 << 2,
  bookSearch: 1 << 3,
  entitySearch: 1 << 4,
  isbnSearch: 1 << 5,
  asinSearch: 1 << 6
};

const lookupCapabilityMask = metadataCapability.authorLookup | metadataCapability.bookLookup;
const searchCapabilityMask = metadataCapability.authorSearch | metadataCapability.bookSearch | metadataCapability.entitySearch | metadataCapability.isbnSearch | metadataCapability.asinSearch;

const legacyMetadataProviderOptions = [
  {
    key: 'openlibrary',
    value: 'Open Library'
  },
  {
    key: 'rreading-glasses',
    value: 'rreading-glasses'
  }
];

function normalizeProviderDescriptor(provider) {
  const key = (provider?.providerKey || provider?.ProviderKey || provider?.key || provider?.Key || '').toLowerCase();

  if (!key) {
    return null;
  }

  const name = provider?.name || provider?.Name || provider?.displayName || provider?.DisplayName || key;
  const capabilities = Number(provider?.capabilities ?? provider?.Capabilities ?? 0);
  const contentTypes = Number(provider?.contentTypes ?? provider?.ContentTypes ?? 0);
  const contentTypeNames = provider?.contentTypeNames || provider?.ContentTypeNames || [];
  const priority = Number(provider?.priority ?? provider?.Priority ?? 0);

  return {
    key,
    value: name,
    capabilities,
    contentTypes,
    contentTypeNames,
    priority
  };
}

function filterProvidersByCapability(providers, capabilityMask) {
  if (!Array.isArray(providers) || providers.length === 0) {
    return legacyMetadataProviderOptions;
  }

  const filtered = providers
    .map(normalizeProviderDescriptor)
    .filter((provider) => provider &&
      (provider.capabilities === 0 || (provider.capabilities & capabilityMask) !== 0));

  if (!filtered.length) {
    return legacyMetadataProviderOptions;
  }

  const unique = filtered.reduce((accumulator, provider) => {
    if (!accumulator.some((otherProvider) => otherProvider.key === provider.key)) {
      accumulator.push(provider);
    }

    return accumulator;
  }, []);

  return unique.sort((a, b) => {
    if (a.priority === b.priority) {
      return a.value.localeCompare(b.value);
    }

    return b.priority - a.priority;
  });
}

function ensureActiveProviderFallback(options, activeProvider) {
  if (!activeProvider) {
    return options;
  }

  const normalizedActiveProvider = activeProvider.toLowerCase();
  const hasFallbackProvider = options.some((provider) => provider.key === normalizedActiveProvider);
  if (hasFallbackProvider) {
    return options;
  }

  const fallbackProvider = legacyMetadataProviderOptions.find((provider) => provider.key === normalizedActiveProvider) || {
    key: normalizedActiveProvider,
    value: normalizedActiveProvider
  };

  return [...options, fallbackProvider];
}

const writeAudioTagOptions = [
  {
    key: 'no',
    get value() {
      return translate('WriteTagsNo');
    }
  },
  {
    key: 'sync',
    get value() {
      return translate('WriteTagsSync');
    }
  },
  {
    key: 'allFiles',
    get value() {
      return translate('WriteTagsAll');
    }
  },
  {
    key: 'newFiles',
    get value() {
      return translate('WriteTagsNew');
    }
  }
];

const writeBookTagOptions = [
  {
    key: 'sync',
    get value() {
      return translate('WriteTagsSync');
    }
  },
  {
    key: 'allFiles',
    get value() {
      return translate('WriteTagsAll');
    }
  },
  {
    key: 'newFiles',
    get value() {
      return translate('WriteTagsNew');
    }
  }
];

function getMetadataSearchProviders(value, fallbackProvider) {
  const normalizedFallback = fallbackProvider?.toLowerCase();

  const providers = value ?
    value
      .split(',')
      .map((provider) => provider.trim().toLowerCase())
      .filter(Boolean) :
    [];

  if (!providers.length) {
    return normalizedFallback ? [normalizedFallback] : [];
  }

  return providers;
}

function buildMetadataSearchProviderValue(previousValues, providerKey, checked, fallbackProvider) {
  const normalizedProvider = providerKey.toLowerCase();
  const values = getMetadataSearchProviders(previousValues, fallbackProvider);
  const selected = new Set(values);

  if (checked) {
    selected.add(normalizedProvider);
  } else {
    selected.delete(normalizedProvider);
  }

  if (selected.size === 0 && fallbackProvider) {
    selected.add(fallbackProvider.toLowerCase());
  }

  return Array.from(selected).join(',');
}

function MetadataProvider(props) {
  const {
    isFetching,
    error,
    settings,
    hasSettings,
    onInputChange
  } = props;

  const activeSearchProviders = getMetadataSearchProviders(
    settings.metadataSearchProviders?.value,
    settings.metadataProvider.value
  );
  const activeSearchProviderSet = new Set(activeSearchProviders);

  const availableMetadataProviders = settings.availableMetadataProviders?.value;
  const metadataProviderOptions = ensureActiveProviderFallback(
    filterProvidersByCapability(availableMetadataProviders, lookupCapabilityMask),
    settings.metadataProvider.value
  );
  const metadataSearchProviderOptions = filterProvidersByCapability(availableMetadataProviders, searchCapabilityMask);

  const onToggleSearchProvider = (providerKey, checked) => {
    onInputChange({
      name: 'metadataSearchProviders',
      value: buildMetadataSearchProviderValue(
        settings.metadataSearchProviders?.value,
        providerKey,
        checked,
        settings.metadataProvider.value
      )
    });
  };

  return (

    <div>
      {
        isFetching &&
          <LoadingIndicator />
      }

      {
        !isFetching && error &&
          <Alert kind={kinds.DANGER}>
            {translate('UnableToLoadMetadataProviderSettings')}
          </Alert>
      }

      {
        hasSettings && !isFetching && !error &&
          <Form>
            <FieldSet legend={translate('CalibreMetadata')}>
              <FormGroup>
                <FormLabel>
                  {translate('MetadataProvider')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.SELECT}
                  name="metadataProvider"
                  values={metadataProviderOptions}
                  helpText={translate('MetadataProviderHelpText')}
                  onChange={onInputChange}
                  {...settings.metadataProvider}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('MetadataSearchProviders')}
                </FormLabel>

                <FormLabel>
                  {translate('MetadataSearchProvidersHelpText')}
                </FormLabel>
              </FormGroup>

              {
                metadataSearchProviderOptions.map((provider) => {
                  return (
                    <FormGroup
                      key={provider.key}
                    >
                      <FormLabel>
                        {provider.value}
                      </FormLabel>

                      <FormInputGroup
                        type={inputTypes.CHECK}
                        name="metadataSearchProviders"
                        onChange={(event) => onToggleSearchProvider(provider.key, event.value)}
                        value={activeSearchProviderSet.has(provider.key)}
                      />
                    </FormGroup>
                  );
                })
              }

              <FormGroup>
                <FormLabel>
                  {translate('MetadataOpenLibrarySource')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="metadataOpenLibrarySource"
                  helpText={translate('MetadataOpenLibrarySourceHelpText')}
                  helpLink="https://openlibrary.org/developers/api"
                  onChange={onInputChange}
                  {...settings.metadataOpenLibrarySource}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('MetadataRreadingGlassesSource')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="metadataRreadingGlassesSource"
                  helpText={translate('MetadataRreadingGlassesSourceHelpText')}
                  helpLink="https://wiki.servarr.com/readarr/settings#metadata"
                  onChange={onInputChange}
                  {...settings.metadataRreadingGlassesSource}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('SendMetadataToCalibre')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.SELECT}
                  name="writeBookTags"
                  helpTextWarning={translate('WriteBookTagsHelpTextWarning')}
                  helpLink="https://wiki.servarr.com/readarr/settings#write-metadata-to-book-files"
                  values={writeBookTagOptions}
                  onChange={onInputChange}
                  {...settings.writeBookTags}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('UpdateCovers')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="updateCovers"
                  helpText={translate('UpdateCoversHelpText')}
                  onChange={onInputChange}
                  {...settings.updateCovers}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('EmbedMetadataInBookFiles')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="embedMetadata"
                  helpText={translate('EmbedMetadataHelpText')}
                  onChange={onInputChange}
                  {...settings.embedMetadata}
                />
              </FormGroup>

            </FieldSet>

            <FieldSet legend={translate('AudioFileMetadata')}>
              <FormGroup>
                <FormLabel>{translate('WriteAudioTags')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.SELECT}
                  name="writeAudioTags"
                  helpTextWarning={translate('WriteBookTagsHelpTextWarning')}
                  helpLink="https://wiki.servarr.com/readarr/settings#write-metadata-to-audio-files"
                  values={writeAudioTagOptions}
                  onChange={onInputChange}
                  {...settings.writeAudioTags}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('WriteAudioTagsScrub')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="scrubAudioTags"
                  helpTextWarning={translate('WriteAudioTagsScrubHelp')}
                  onChange={onInputChange}
                  {...settings.scrubAudioTags}
                />
              </FormGroup>

            </FieldSet>
          </Form>
      }
    </div>

  );
}

MetadataProvider.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired
};

export default MetadataProvider;
