
pipeline {
    agent {
        label 'linux && docker && dotnet && angular'
    }

    options {
        timestamps()
        timeout(time: 30, unit: 'MINUTES')
        disableConcurrentBuilds()
    }

    environment {
        API_PROJECT = 'src/ScalingDemo.Api/ScalingDemo.Api.csproj'
        WEB_DIR = 'src/ScalingDemo.Web'

        API_IMAGE = 'bmi-health-api'
        WEB_IMAGE = 'bmi-health-web'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm

                sh '''
                    echo "Repository: BMI Health System"
                    git rev-parse --short HEAD
                '''
            }
        }

        stage('Validate Tools and Files') {
            steps {
                sh '''
                    set -eu

                    test -f "$API_PROJECT"
                    test -f "$WEB_DIR/package.json"
                    test -f "$WEB_DIR/package-lock.json"
                    test -f "$WEB_DIR/angular.json"

                    test -f src/ScalingDemo.Api/Dockerfile
                    test -f "$WEB_DIR/Dockerfile"

                    dotnet --version
                    node --version
                    npm --version
                    docker --version

                    echo "Validation successful."
                '''
            }
        }

        stage('.NET Restore') {
            steps {
                sh 'dotnet restore "$API_PROJECT"'
            }
        }

        stage('.NET Build') {
            steps {
                sh '''
                    dotnet build "$API_PROJECT" \
                        --configuration Release \
                        --no-restore
                '''
            }
        }

        stage('Angular Dependencies') {
            steps {
                dir('src/ScalingDemo.Web') {
                    sh 'npm ci'
                }
            }
        }

        stage('Angular Production Build') {
            steps {
                dir('src/ScalingDemo.Web') {
                    sh 'npm run build -- --configuration production'
                }
            }
        }

        stage('Build API Docker Image') {
            steps {
                sh '''
                    docker build \
                        --tag "$API_IMAGE:$BUILD_NUMBER" \
                        --file src/ScalingDemo.Api/Dockerfile \
                        src/ScalingDemo.Api
                '''
            }
        }

        stage('Build Frontend Docker Image') {
            steps {
                sh '''
                    docker build \
                        --tag "$WEB_IMAGE:$BUILD_NUMBER" \
                        --file src/ScalingDemo.Web/Dockerfile \
                        src/ScalingDemo.Web
                '''
            }
        }

        stage('Verify Images') {
            steps {
                sh '''
                    docker image inspect "$API_IMAGE:$BUILD_NUMBER"
                    docker image inspect "$WEB_IMAGE:$BUILD_NUMBER"

                    docker images "$API_IMAGE"
                    docker images "$WEB_IMAGE"
                '''
            }
        }
    }

    post {
        success {
            echo 'SUCCESS: BMI Health Tracker CI completed.'
        }

        failure {
            echo 'FAILURE: Check Console Output for the failed stage.'
        }

        always {
            echo "Final result: ${currentBuild.currentResult}"
        }
    }
}
