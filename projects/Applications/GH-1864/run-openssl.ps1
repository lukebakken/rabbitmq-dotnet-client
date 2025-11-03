& openssl.exe s_client -connect localhost:5671 -CAfile .\certs\chained_ca_certificate.pem -cert .\certs\client_certificate.pem -key .\certs\\client_key.pem
