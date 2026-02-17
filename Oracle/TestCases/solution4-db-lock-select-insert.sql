declare
    i_a             t_data.a%type := :i_a;
    i_b             t_data.b%type := :i_b;
    o_id            t_data.id%type;

    l_lock_handle           varchar2(128);
    l_lock_request_result   integer;
begin
    insert into t_data_lock (a, b)
    values (i_a, i_b);

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

    delete from t_data_lock
    where a = i_a and b = i_b;
end;
