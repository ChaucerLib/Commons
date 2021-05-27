using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace Commons.Aws
{
    public static class Config
    {
        public static (AWSCredentials credentials, RegionEndpoint regionEndpoint) GetAwsConfig(string awsProfileName, string region)
        {
            if (string.IsNullOrWhiteSpace(awsProfileName)) throw new ArgumentNullException(nameof(awsProfileName));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentNullException(nameof(region));
            
            var credProfileStoreChain = new CredentialProfileStoreChain();
            if (credProfileStoreChain.TryGetAWSCredentials(awsProfileName, out var awsCredentials))
            {
                return (awsCredentials, RegionEndpoint.GetBySystemName(region));
            }

            throw new ArgumentException($"{awsProfileName} was not a profile available in the credentials store");
        }
    }
}