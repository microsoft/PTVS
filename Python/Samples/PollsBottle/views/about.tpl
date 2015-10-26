% rebase('layout.tpl', title=title, year=year)

<h2>{{title}}.</h2>

<p>This is a sample polls application that demonstrates the use of Bottle web framework.</p>
<p>The application can be configured to use one of the following repositories: Azure Table Storage, MongoDB or In-Memory.</p>
<p>Current repository: <b>{{repository_name}}</b></p>
