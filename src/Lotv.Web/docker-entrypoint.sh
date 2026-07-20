#!/bin/sh
# Blazor WASM is static files served by nginx here, so config can't come from
# an environment variable at runtime the normal ASP.NET way — the browser
# only ever sees whatever is in wwwroot/appsettings.json. This writes that
# file from $API_BASE_URL just before nginx starts, so the same image can be
# deployed to staging/production with different API hosts via `docker run -e`
# / the hosting platform's env config, without rebuilding.
set -e

: "${API_BASE_URL:=http://localhost:5275}"

cat > /usr/share/nginx/html/appsettings.json <<EOF
{
  "ApiBaseUrl": "$API_BASE_URL"
}
EOF

exec nginx -g 'daemon off;'
