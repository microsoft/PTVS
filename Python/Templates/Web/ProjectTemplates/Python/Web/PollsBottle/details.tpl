% rebase('layout.tpl', title='Poll Page', year=year)

<h2>{{poll.text}}</h2>
<br/>

%if error_message:
<p class="text-danger">{{ error_message }}</p>
%end

<form action="/poll/{{poll.key}}" method="post">
    %for choice in poll.choices:
    <div class="radio">
        <label>
            <input type="radio" name="choice" id="choice{{choice.key}}" value="{{choice.key}}" />
            {{choice.text}}
        </label>
    </div>
    %end
    <br/>
    <button class="btn btn-primary" type="submit">Vote</button>
</form>
