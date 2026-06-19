targetScope = 'local'

extension az
extension local

resource getKubeConfig 'Script' = {
  type: 'Bash'
  script: 'kubectl config view --raw'
}

module aksStoreApp 'helm.bicep' = {
  params: {
    kubeConfig: base64(getKubeConfig.stdOut)
  }
}
