using System.Collections.Generic;

using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.Validation;
using Readarr.Http;

namespace Readarr.Api.V3.Config
{
    [V1ApiController("config/metadataprovider")]
    public class MetadataProviderConfigController : ConfigController<MetadataProviderConfigResource>
    {
        private readonly IEnumerable<IMetadataProviderV1> _metadataProviders;

        public MetadataProviderConfigController(
            IConfigService configService,
            IEnumerable<IMetadataProviderV1> metadataProviders)
            : base(configService)
        {
            _metadataProviders = metadataProviders;
            SharedValidator.RuleFor(c => c.MetadataOpenLibrarySource).IsValidUrl().When(c => !c.MetadataOpenLibrarySource.IsNullOrWhiteSpace());
            SharedValidator.RuleFor(c => c.MetadataRreadingGlassesSource).IsValidUrl().When(c => !c.MetadataRreadingGlassesSource.IsNullOrWhiteSpace());
        }

        protected override MetadataProviderConfigResource ToResource(IConfigService model)
        {
            return MetadataProviderConfigResourceMapper.ToResource(model, _metadataProviders);
        }
    }
}
