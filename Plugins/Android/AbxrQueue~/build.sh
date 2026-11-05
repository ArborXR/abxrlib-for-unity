#!/bin/sh
# Builds Plugins/Android/AbxrQueue.aar from src/ (offline RFC D13: the queue's SQLite store on Android).
# Unity ignores this folder (its name ends in ~); commit the rebuilt .aar with any change to the Java source.
#   JAVA_HOME     a JDK, e.g. Unity's PlaybackEngines/AndroidPlayer/OpenJDK
#   ANDROID_JAR   an Android platform's android.jar, e.g. $ANDROID_HOME/platforms/android-34/android.jar
set -e
cd "$(dirname "$0")"
: "${JAVA_HOME:?set JAVA_HOME to a JDK}"
: "${ANDROID_JAR:?set ANDROID_JAR to the android.jar of an Android platform}"
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
mkdir -p "$out/classes" "$out/aar"
"$JAVA_HOME/bin/javac" -source 8 -target 8 -nowarn -Xlint:-options -bootclasspath "$ANDROID_JAR" \
    -d "$out/classes" $(find src -name '*.java')
"$JAVA_HOME/bin/jar" --create --file "$out/aar/classes.jar" --date=2026-01-01T00:00:00Z -C "$out/classes" .
cp AndroidManifest.xml proguard.txt "$out/aar/"
: > "$out/aar/R.txt"
rm -f ../AbxrQueue.aar
(cd "$out/aar" && "$JAVA_HOME/bin/jar" --create --no-manifest --file "$OLDPWD/../AbxrQueue.aar" --date=2026-01-01T00:00:00Z .)
echo "Built $(cd .. && pwd)/AbxrQueue.aar"
