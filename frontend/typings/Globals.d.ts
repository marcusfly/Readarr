declare module '*.module.css';

// Untyped third-party packages — minimal stubs to satisfy noImplicitAny.
// Replace with proper @types packages when available.
declare module 'history';
declare module 'react-router-dom';
declare module 'react-router';
declare module 'connected-react-router';
declare module 'redux-localstorage';
declare module 'redux-batched-actions';
declare module 'react-autosuggest';
declare module 'react-document-title';
declare module 'react-measure';
declare module 'react-middle-truncate';
declare module 'react-popper';
declare module 'react-async-script';
declare module 'react-google-recaptcha';
declare module 'react-lazyload';
declare module 'react-text-truncate';
declare module 'react-custom-scrollbars-2';
declare module 'react-addons-shallow-compare';
declare module 'element-class';
declare module 'mousetrap';
declare module 'mobile-detect';
declare module 'stacktrace-js';
declare module 'clipboard';

interface Window {
  Readarr: {
    apiKey: string;
    instanceName: string;
    theme: string;
    urlBase: string;
    apiRoot: string;
    version: string;
    isProduction: boolean;
  };
}
