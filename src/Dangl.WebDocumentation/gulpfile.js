/// <binding Clean='clean' />
"use strict";

var gulp = require("gulp"),
    rimraf = require("rimraf"),
    concat = require("gulp-concat"),
    cssmin = require("gulp-cssmin"),
    uglify = require("gulp-uglify"),
    sass = require("gulp-sass");

var paths = {
    webroot: "./wwwroot/"
};

paths.js = paths.webroot + "js/**/*.js";
paths.minJs = paths.webroot + "js/**/*.min.js";
paths.css = paths.webroot + "css/**/*.css";
paths.sass = paths.webroot + "css/**/*.scss";
paths.minCss = paths.webroot + "css/**/*.min.css";
paths.concatJsDest = paths.webroot + "js/site.min.js";
paths.concatCssDest = paths.webroot + "css/site.min.css";
var frontEndPackages = ["jquery", "bootstrap"];

gulp.task("copy:lib",
    function (cb) {
        gulp.src([
                "./node_modules/jquery/**/*"
            ])
            .pipe(gulp.dest("./wwwroot/lib/jquery"));
        return gulp.src([
                "./node_modules/bootstrap/**/*"
            ])
            .pipe(gulp.dest("./wwwroot/lib/bootstrap"));
    });

gulp.task("clean:js", function (cb) {
    rimraf(paths.concatJsDest, cb);
});

gulp.task("clean:css", function (cb) {
    rimraf(paths.concatCssDest, cb);
});

gulp.task("clean:lib", function (cb) {
    rimraf("./wwwroot/lib", cb);
});

gulp.task("clean", ["clean:js", "clean:css", "clean:lib"]);

gulp.task("min:js", function () {
    return gulp.src([paths.js, "!" + paths.minJs], { base: "." })
        .pipe(concat(paths.concatJsDest))
        .pipe(uglify())
        .pipe(gulp.dest("."));
});

gulp.task("min:css", function () {
    return gulp.src([paths.css, "!" + paths.minCss])
        .pipe(concat(paths.concatCssDest))
        .pipe(cssmin())
        .pipe(gulp.dest("."));
});

gulp.task("sass", function () {
    return gulp.src(paths.sass)
        .pipe(sass.sync().on('error', sass.logError))
        .pipe(gulp.dest("./wwwroot/css/"));
});

gulp.task("min", ["min:js", "sass", "min:css"]);
