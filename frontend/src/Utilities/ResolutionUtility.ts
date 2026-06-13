const resolutions = {
  desktopLarge: 1200,
  desktop: 992,
  tablet: 768,
  mobile: 480,
} as const;

function getWidth(): number {
  return window.innerWidth;
}

const ResolutionUtility = {
  resolutions,

  isDesktopLarge(): boolean {
    return getWidth() < resolutions.desktopLarge;
  },

  isDesktop(): boolean {
    return getWidth() < resolutions.desktop;
  },

  isTablet(): boolean {
    return getWidth() < resolutions.tablet;
  },

  isMobile(): boolean {
    return getWidth() < resolutions.mobile;
  },
};

export default ResolutionUtility;
