﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class ProjectsControllerTests : IClassFixture<InMemoryControllerTestsFixture>
    {
        public ProjectsControllerTests(InMemoryControllerTestsFixture fixture)
        {
            UserManager = fixture.UserManager;
            fixture.Context.Database.EnsureDeleted();
            fixture.Context.Database.EnsureCreated();
        }

        private UserManager<ApplicationUser> UserManager { get; }

        private readonly Mock<IProjectsService> _projectsServiceMock = new Mock<IProjectsService>();
        private readonly Mock<IProjectFilesService> _projectFilesServiceMock = new Mock<IProjectFilesService>();
        private readonly Mock<IProjectVersionsService> _projectVersionsServiceMock = new Mock<IProjectVersionsService>();
        private readonly Mock<IDocuUserInfoService> _docuUserInfoService = new Mock<IDocuUserInfoService>();

        private ProjectsController GetController()
        {
            var controller = new ProjectsController(UserManager,
                _projectFilesServiceMock.Object,
                _projectVersionsServiceMock.Object,
                _projectsServiceMock.Object,
                _docuUserInfoService.Object);
            return controller;
        }

        [Fact]
        public async Task RedirectToEntryIfNoFilePathGiven()
        {
            _projectsServiceMock.Setup(s => s.UserHasAccessToProjectAsync(It.IsAny<string>(), It.IsAny<Guid?>()))
                .Returns(Task.FromResult(true));
            _projectFilesServiceMock.Setup(s => s.GetEntryFilePathForProjectAsync(It.IsAny<string>()))
                .Returns(Task.FromResult("index.html"));

            var controller = GetController();
            var result = await controller.GetFile("DemoProject", "v1.0.0", null);
            Assert.IsType<RedirectToActionResult>(result);
            var redirectResult = result as RedirectToActionResult;
            Assert.Equal("GetFile", redirectResult.ActionName);
            Assert.Equal("DemoProject", redirectResult.RouteValues["projectName"]);
            Assert.Equal("v1.0.0", redirectResult.RouteValues["version"]);
            Assert.Equal("index.html", redirectResult.RouteValues["pathToFile"]);
        }

        [Fact]
        public async Task RedirectToHigherVersionIfVersionNotPresent()
        {
            _projectsServiceMock.Setup(s => s.UserHasAccessToProjectAsync(It.IsAny<string>(), It.IsAny<Guid?>()))
                .Returns(Task.FromResult(true));
            _projectVersionsServiceMock.Setup(s => s.GetProjectVersionsAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(new List<(string, bool, bool, DateTimeOffset?)> {("v1.0.0",false, false, null), ("v1.0.1", false, false, null) }));

            var controller = GetController();
            var result = await controller.GetFile("DemoProject", "v0.0.1", "index.html");
            Assert.IsType<RedirectToActionResult>(result);
            var redirectResult = result as RedirectToActionResult;
            Assert.Equal("GetFile", redirectResult.ActionName);
            Assert.Equal("DemoProject", redirectResult.RouteValues["projectName"]);
            Assert.Equal("v1.0.0", redirectResult.RouteValues["version"]);
            Assert.Equal("index.html", redirectResult.RouteValues["pathToFile"]);
        }

        [Fact]
        public async Task ReturnNotFoundIfVersionNotPresentAndNoHigherVersion()
        {
            _projectsServiceMock.Setup(s => s.UserHasAccessToProjectAsync(It.IsAny<string>(), It.IsAny<Guid?>()))
                .Returns(Task.FromResult(true));
            _projectVersionsServiceMock.Setup(s => s.GetProjectVersionsAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(new List<(string, bool, bool, DateTimeOffset?)> { ("v1.0.0", false, false, null), ("v1.0.1", false, false, null) }));

            var controller = GetController();
            var result = await controller.GetFile("DemoProject", "v1.0.2", "index.html");
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnRedirectToIndexIfVersionCorrectButFileNotFound()
        {
            _projectsServiceMock.Setup(s => s.UserHasAccessToProjectAsync(It.IsAny<string>(), It.IsAny<Guid?>()))
                .Returns(Task.FromResult(true));
            _projectVersionsServiceMock.Setup(s => s.GetProjectVersionsAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(new List<(string, bool, bool, DateTimeOffset?)> { ("v1.0.0", false, false, null), ("v1.0.1", false, false, null) }));
            _projectFilesServiceMock.Setup(s => s.GetEntryFilePathForProjectAsync(It.IsAny<string>()))
                .Returns(Task.FromResult("index.html"));

            var controller = GetController();
            var result = await controller.GetFile("DemoProject", "v1.0.0", "not_found.html");
            Assert.IsType<RedirectToActionResult>(result);
            var redirectResult = result as RedirectToActionResult;
            Assert.Equal("GetFile", redirectResult.ActionName);
            Assert.Equal("DemoProject", redirectResult.RouteValues["projectName"]);
            Assert.Equal("v1.0.0", redirectResult.RouteValues["version"]);
            Assert.Equal("index.html", redirectResult.RouteValues["pathToFile"]);
        }
    }
}
