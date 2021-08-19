//Jenkinsfile 20210518
String newversion = null
try {
    node ('master') {
        stage('Checkout and prepare version'){
            cleanWs()
            checkout scm
            withCredentials([file(credentialsId: 'TraegerLicenseSinumerikSDKEval', variable: 'FILE')]) {
                sh 'cp $FILE > src/Ctrl2MqttBridge'
            }
            newversion = getNewVersion('main', 2, true)
        }
        stage('Build windows binaries') {
            script {
                def dotnet = docker.image('mcr.microsoft.com/dotnet/sdk')
                dotnet.pull()
                dotnet.inside('-u 0:0') {
                    sh "dotnet publish --configuration Release --runtime win-x86 -f net50 -p:Version=${newversion} -o publishwinx86 src/Ctrl2MqttBridge/Ctrl2MqttBridge.csproj"
                    sh 'chown -R 1002:1002 .'
                }
            }
        }
        stage('Create windows installer') {
            script {
                sh('cp nsis/installer.nsi nsis/installerv.nsi')
                sh("sed -i 's/VIProductVersion \"2.0.0.0\"/VIProductVersion \"${newversion}.0\"/g' nsis/installerv.nsi")
                sh("sed -i 's/VIFileVersion \"2.0.0.0\"/VIFileVersion \"${newversion}.0\"/g' nsis/installerv.nsi")
                def nsis=docker.image('binfalse/nsis')
                nsis.pull()
                nsis.inside("--entrypoint=''"){
                    sh "makensis -V4 nsis/installerv.nsi"
                }
                
            }
        }
        stage('Upload windows installer to nextcloud') {
            uploadToNextcloud("nsis/Ctrl2MqttBridgeSetup.exe","Public/Ctrl2MqttBridge/Ctrl2MqttBridgeSetup_${env.BRANCH_NAME}_${newversion}.exe")
        }
        stage('Build docker image') {
            script {
                def tagname=newversion.tokenize('+')[0]
                docker.withRegistry(
                    'https://852118034105.dkr.ecr.eu-central-1.amazonaws.com',
                    'ecr:eu-central-1:awsecrfull') {
                    def dockerImage = docker.build("dvs-edge/ctrl2mqttbridge", "--build-arg newversion=${newversion} .")
                    dockerImage.push('latest')
                }
                sh "docker tag dvs-edge/ctrl2mqttbridge dvs-edge/ctrl2mqttbridge:${tagname}"
                sh 'docker save --output dvs-edge.ctrl2mqttbridge.docker.tar dvs-edge/ctrl2mqttbridge'
                sh "docker image rm dvs-edge/ctrl2mqttbridge:${tagname}"
                sh 'docker image rm dvs-edge/ctrl2mqttbridge'
            }
        }
        stage('upload docker.tar to nextcloud') {
            uploadToNextcloud("dvs-edge.ctrl2mqttbridge.docker.tar","Public/Ctrl2MqttBridge/ctrl2mqttbridge_${env.BRANCH_NAME}.docker.tar")
        }
        stage('Tag repo and clean') {
            def versionfile="ctrl2mqttbridge_${env.BRANCH_NAME}.version.txt"
            sh "echo ${newversion} > ${versionfile}"
            uploadToNextcloud(versionfile,"Public/Ctrl2MqttBridge/${versionfile}")
            withCredentials([usernamePassword(credentialsId: 'githubtoken', usernameVariable: 'USERNAME', passwordVariable: 'TOKEN')]) {
                sh 'git remote set-url origin https://$TOKEN@github.com/andreaskueffel/Ctrl2MqttBridge.git'   
            }
            tagAndPush(newversion)
            cleanWs()
        }
    }
}
finally {
    if (currentBuild.result == 'SUCCESS') {
            mail to: 'florian.schlarbaum@praewema.de, andreas.kueffel@praewema.de',
            cc: '',
            bcc: '',
            from: 'Jenkins (do-not-reply@praewema.de)',
            subject: "[Jenkins] NEW Praewema Ctrl2MqttBridge ${newversion} - ${env.JOB_NAME} released",
            body: "<b>${env.JOB_NAME}</b><br>Build no. ${env.BUILD_Number} succeeded! <br> URL: ${env.BUILD_URL} <br><br>New Ctrl2MqttBridge released.<br> " + getChangeString() + " ",
            mimeType: 'text/html',
            charset: 'UTF-8'; 
    }
    emailext attachLog: true,
                     body: "<b>${env.JOB_NAME}</b><br>Build no. ${env.BUILD_Number} has status <b>${currentBuild.currentResult}</b>! <br><br>More Info: <br> ${env.BUILD_URL}<br><br><br>" + getChangeString() + ' ',
                     recipientProviders: [developers(), requestor()],
                     subject: "[Jenkins] Build-Pipeline ${env.JOB_NAME} - ${currentBuild.currentResult}"

}

