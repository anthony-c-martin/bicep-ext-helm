targetScope = 'local'

@secure()
param kubeConfig string

extension helm with {
  kubeConfig: kubeConfig
}

resource release 'Release' = {
  name: 'nginx-ingress-controller'
  repository: 'https://charts.bitnami.com/bitnami'
  chart: 'nginx-ingress-controller'
  set: [
    {
      name: 'service.type'
      value: 'ClusterIP'
    }
  ]
}
