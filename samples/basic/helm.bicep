targetScope = 'local'

@secure()
param kubeConfig string

extension helm with {
  kubeConfig: kubeConfig
}

resource release 'Release' = {
  name: 'azure-vote'
  repository: 'https://azure-samples.github.io/helm-charts'
  chart: 'azure-vote'
  set: [
    {
      name: 'title'
      value: 'Do you love Bicep?'
    }
    {
      name: 'value1'
      value: 'Of course!'
    }
    {
      name: 'image.tag'
      value: 'Nope :('
    }
  ]
}
