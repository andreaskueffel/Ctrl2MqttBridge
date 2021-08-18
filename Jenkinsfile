//Jenkinsfile 20210518
String newversion = null
try {
    node ('master') {
        stage('Checkout and prepare Version'){
            cleanWs()
            checkout scm
            newversion = getNewVersion()
        }
        stage('Build Windows binaries') {
            script {
                def dotnet = docker.image('mcr.microsoft.com/dotnet/sdk')
                dotnet.pull()
                dotnet.inside('-u 0:0') {
                    sh "dotnet publish --configuration Release --runtime win-x86 -f net50 -p:Version=${newversion} -o publishwinx86 src/Ctrl2MqttBridge/Ctrl2MqttBridge.csproj"
                    sh 'chown -R 1002:1002 .'
                }
                sh 'cd publishwinx86 && zip -r publish.zip .'
            }
        }
        stage('upload publishwinx86.zip to nextcloud') {
            uploadToNextcloud("publishwinx86/publish.zip","DataPort/Module/ctrl2mqttbridge_${env.BRANCH_NAME}_${newversion}.windows.zip")
        }
        stage('build docker image') {
            script {
                def tagname=newversion.tokenize('+')[0]
                def dockerImage = docker.build("dvs-ede/ctrl2mqttbridge:${tagname}", "--build-arg newversion=${newversion} .")
                sh "docker tag dvs-ede/ctrl2mqttbridge:${tagname} dvs-ede/ctrl2mqttbridge:latest"
                sh 'docker save --output dvs-edge.ctrl2mqttbridge.docker.tar dvs-ede/ctrl2mqttbridge'
                sh "docker image rm dvs-ede/ctrl2mqttbridge:${tagname}"
                sh 'docker image rm dvs-ede/ctrl2mqttbridge:latest'
            }
        }
        stage('upload docker.tar to nextcloud') {
            uploadToNextcloud("dvs-edge.ctrl2mqttbridge.docker.tar","DataPort/Module/ctrl2mqttbridge_${env.BRANCH_NAME}.docker.tar")
        }
        stage('Tag repo and clean') {
            def versionfile="ctrl2mqttbridge__${env.BRANCH_NAME}.version.txt"
            sh "echo ${newversion} > ${versionfile}"
            uploadToNextcloud(versionfile,"DataPort/Module/${versionfile}")
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

