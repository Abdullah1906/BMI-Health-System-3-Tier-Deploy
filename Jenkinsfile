pipeline {
    agent {
        label 'linux-agent'
    }

    options {
        timestamps()
        timeout(time: 30, unit: 'MINUTES')
        disableConcurrentBuilds()
    }

    environment {
        API_PROJECT = 'src/ScalingDemo.Api/ScalingDemo.Api.csproj'
        WEB_DIR = 'src/ScalingDemo.Web'

        DOCKERHUB_NAMESPACE = 'abdullah1438'

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
                        --tag "$DOCKERHUB_NAMESPACE/$API_IMAGE:$BUILD_NUMBER" \
                        --tag "$DOCKERHUB_NAMESPACE/$API_IMAGE:latest" \
                        --file src/ScalingDemo.Api/Dockerfile \
                        .
                '''
            }
        }

        stage('Build Frontend Docker Image') {
            steps {
                sh '''
                    docker build \
                        --tag "$DOCKERHUB_NAMESPACE/$WEB_IMAGE:$BUILD_NUMBER" \
                        --tag "$DOCKERHUB_NAMESPACE/$WEB_IMAGE:latest" \
                        --file src/ScalingDemo.Web/Dockerfile \
                        src/ScalingDemo.Web
                '''
            }
        }

        stage('Verify Images') {
            steps {
                sh '''
                    docker image inspect "$DOCKERHUB_NAMESPACE/$API_IMAGE:$BUILD_NUMBER"
                    docker image inspect "$DOCKERHUB_NAMESPACE/$WEB_IMAGE:$BUILD_NUMBER"

                    docker images "$DOCKERHUB_NAMESPACE/$API_IMAGE"
                    docker images "$DOCKERHUB_NAMESPACE/$WEB_IMAGE"
                '''
            }
        }

        stage('Push Images to Docker Hub') {
            steps {
                withCredentials([
                    usernamePassword(
                        credentialsId: 'dockerhub-credentials',
                        usernameVariable: 'DOCKERHUB_USER',
                        passwordVariable: 'DOCKERHUB_TOKEN'
                    )
                ]) {
                    sh '''
                        set -eu
                        set +x

                        trap 'docker logout || true' EXIT

                        printf '%s' "$DOCKERHUB_TOKEN" |
                            docker login \
                                --username "$DOCKERHUB_USER" \
                                --password-stdin

                        docker push \
                            "$DOCKERHUB_NAMESPACE/$API_IMAGE:$BUILD_NUMBER"

                        docker push \
                            "$DOCKERHUB_NAMESPACE/$API_IMAGE:latest"

                        docker push \
                            "$DOCKERHUB_NAMESPACE/$WEB_IMAGE:$BUILD_NUMBER"

                        docker push \
                            "$DOCKERHUB_NAMESPACE/$WEB_IMAGE:latest"
                    '''
                }
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
