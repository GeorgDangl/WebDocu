pipeline {
    agent {
        node {
            label 'master'
            customWorkspace 'workspace/WebDocu'
        }
    }
    environment {
        WebDeployCredentials = credentials('Danglserver3DeployCredentials')
        Danglserver3DeployEndpoint = credentials('Danglserver3DeployEndpoint')
    }
    stages {
        stage ('Test') {
            steps {
                powershell './build.ps1 Coverage -Configuration Debug'
            }
            post {
                always {
                   warnings(
                       canComputeNew: false,
                       canResolveRelativePaths: false,
                       categoriesPattern: '',
                       consoleParsers: [[parserName: 'MSBuild']],
                       defaultEncoding: '',
                       excludePattern: '',
                       healthy: '',
                       includePattern: '',
                       messagesPattern: '',
                       unHealthy: '')
                   openTasks(
                       canComputeNew: false,
                       defaultEncoding: '',
                       excludePattern: 'Dangl.WebDocumentation/node_modules/**/*',
                       healthy: '',
                       high: 'HACK, FIXME',
                       ignoreCase: true,
                       low: '',
                       normal: 'TODO',
                       pattern: '**/*.cs, **/*.g4, **/*.ts, **/*.js',
                       unHealthy: '')
                   xunit(
                       testTimeMargin: '3000',
                       thresholdMode: 1,
                       thresholds: [
                           failed(failureNewThreshold: '0', failureThreshold: '0', unstableNewThreshold: '0', unstableThreshold: '0'),
                           skipped(failureNewThreshold: '0', failureThreshold: '0', unstableNewThreshold: '0', unstableThreshold: '0')
                       ],
                       tools: [
                           xUnitDotNet(deleteOutputFiles: true, failIfNotNew: true, pattern: '**/*testresults.xml', stopProcessingIfError: true)
                       ])
                   cobertura(
                       coberturaReportFile: 'output/cobertura.xml',
                       failUnhealthy: false,
                       failUnstable: false,
                       maxNumberOfBuilds: 0,
                       onlyStable: false,
                       zoomCoverageChart: false)
                   publishHTML([
                       allowMissing: false,
                       alwaysLinkToLastBuild: false,
                       keepAll: false,
                       reportDir: 'output/CoverageReport',
                       reportFiles: 'index.htm',
                       reportName: 'Coverage Report',
                       reportTitles: ''])
                }
            }
        }
        stage ('Deploy') {
            when {
                branch 'master'
            }
            steps {
                script {
                    env.WebDeploySiteName = 'WebDocu'
                    env.WebDeployPublishUrl = env.Danglserver3DeployEndpoint + '/msdeploy.axd?site=WebDocu'
                    env.WebDeployPassword = env.WebDeployCredentials_PSW
                    env.WebDeployUsername = env.WebDeployCredentials_USR
                }
                powershell './build.ps1 Deploy'
            }
        }
    }
    post {
        always {
            step([$class: 'Mailer',
                notifyEveryUnstableBuild: true,
                recipients: "georg@dangl.me",
                sendToIndividuals: true])
        }
    }
}
