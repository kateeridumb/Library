DO 
DECLARE rid int;
BEGIN
  SELECT roleid INTO rid FROM roles WHERE rolename='Admin' LIMIT 1;
  IF rid IS NULL THEN
    INSERT INTO roles(rolename) VALUES ('Admin') RETURNING roleid INTO rid;
  END IF;
  INSERT INTO users(username,passwordhash,passwordsalt,firstname,lastname,email,roleid,isblocked)
  VALUES ('e2e_admin_1771185421','X2ZS9L22Cl5mXnDayKjEtTE6T7OwlIhA7aMcfn65m+MGa6TCN/mVs3MJqztnLZUPPa6lJR0XERUY2SyI9VH0Bg==','5LGWcxDu9emzBgSlTDOaD4ya2p4P+p+AzXmaHRrtifw=','Admin',convert_to('Admin','UTF8'),'e2e_admin_1771185421@gmail.com',rid,false)
  ON CONFLICT (username) DO NOTHING;
END ;
