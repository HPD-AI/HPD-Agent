// Global type alias to make ProviderConfig available in the HPD.Agent namespace
// This allows consumers to use ProviderConfig without explicitly importing HPD.Providers.Core

global using ProviderConfig = HPD.Providers.Core.ProviderConfig;
