const PROXY_CONFIG = [
  {
    context: [
      "/Api",
      "/proxy",
    ],
    //pathRewrite: { '^/api': '' },
    target: "https://localhost:7257",
    secure: false,
    ws: true
  }
]

module.exports = PROXY_CONFIG;
