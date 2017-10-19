# Dangl.WebDocumentation - Readme

This is a small web application that I use to host generated project documentation in Html format. It allows to control access to whom can access a project, either by making projects publicly available or by enabling access to projects for specific users.  
It does accept a zip file as input and will then make the content available to be browsable through the web. You can use, for example, [Sharpdox](https://github.com/Geaz/sharpDox) to create html documentation for C# projects, zip the output and upload it as project.

## Setup

Configuration expects a connection string in the appsettings at `Data:DefaultConnection:ConnectionString` and two variables, `AllowUserRegistration` and `SiteTitle`. Upon deployment, the first user to register is granted the admin role. For other users, admin roles are assigned manually by an existing admin.

## Configuration

Admins create and manage projects, set which users can access them and upload packages via the web interface. 

## Upload a package

Projects are expected in zip format. The zip archives content is copied on the server under the App_Data folder. Projects have a property defining which relative path to use initially, e.g. `index.html`.

### Via the web interface
Admins can simply select Upload Package in the admin section for a project and upload a zip file.

### Via the API
In the project edit section, the API key for a project is set, it is used for http uploads.
Example with cURL:  
`curl -F "ApiKey=<YourApiKey>" -F "Version=<DocVersion>" -F "ProjectPackage=@\"<PathToZipPackage>\"" http://<YourDomain>/API/Projects/Upload`

## Access a package

Project names are required to be unique for pretty urls. Access is routed via `http://<YourDomain>/Projects/<ProjectName>/<PathToIndex>`

## License

[View](License.md)
