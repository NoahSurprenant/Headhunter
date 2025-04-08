const PROXY_CONFIG = [
  {
    context: [
      "/api/"
    ],
    //pathRewrite: { '^/api': '' },
    target: "https://localhost:7257",
    secure: false,
    ws: true
  }
]

module.exports = PROXY_CONFIG;
