declare
    i_a             t_data.a%type := :i_a;
    i_b             t_data.b%type := :i_b;
    o_id            t_data.id%type;

    l_lock_handle           varchar2(128);
    l_lock_request_result   integer;
begin
    dbms_lock.allocate_unique_autonomous(
        lockname => 'a:'||i_a||'|b:'||i_b,
        lockhandle => l_lock_handle
    );

    -- https://docs.oracle.com/en/database/oracle/oracle-database/19/arpls/DBMS_LOCK.html#GUID-CC3AEC00-CBFF-45DD-99C3-C7A312C0213E
    l_lock_request_result := dbms_lock.request(
        lockhandle => l_lock_handle,
        release_on_commit => true
    );

    if l_lock_request_result not in (0, 4) then
        raise_application_error(-20000, 'Failed to acquire lock on record (a = '||i_a||', b = '||i_b||') with result of '||l_lock_request_result);
    end if;

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
end;
