% rebase('layout.tpl', title='Home Page', year=year)

<h2>{{title}}.</h2>

%if polls:
<table class="table table-hover">
    <tbody>
        %for poll in polls:
        <tr>
            <td>
                <a href="/poll/{{poll.key}}">{{poll.text}}</a>
            </td>
        </tr>
        %end
    </tbody>
</table>
%else:
<p>No polls available.</p>
<br/>
<form action="/seed" method="post">
    <button class="btn btn-primary" type="submit">Create Sample Polls</button>
</form>
%end
