declare
    i_a             t_data.a%type := :i_a;
    i_b             t_data.b%type := :i_b;
    o_id            t_data.id%type;
begin
    select id
    into o_id
    from t_data
    where a = i_a and b = i_b;
exception
    when no_data_found then
        insert into t_data (a, b)
        values (i_a, i_b)
        returning id into o_id;
end;
