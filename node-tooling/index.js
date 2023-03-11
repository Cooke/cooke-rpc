#!/usr/bin/env node

const https = require("https");
const http = require("http");
const fs = require("fs");
const path = require("path");

const [command, rpcUrl, outputFilePath] = [
  process.argv[2],
  process.argv[3],
  process.argv[4],
];

function printSyntax() {
  console.info("Syntax: cooke-rpc generate <http endpoint> <output file>");
}

if (command !== "generate") {
  console.error("Unknown command.");
  printSyntax();
  return;
}

if (!rpcUrl || !outputFilePath) {
  console.error("Incorrect input paramters.");
  printSyntax();
  return;
}

const outputDir = path.dirname(outputFilePath);
if (!fs.existsSync(outputDir)) {
  fs.mkdirSync(outputDir);
}

(rpcUrl.startsWith("https") ? https : http)
  .request(rpcUrl + "/introspection", (res) => {
    let data = "";
    res.on("data", (d) => (data += d));

    res.on("end", () => {
      const meta = JSON.parse(data);
      generateRpcTs(meta);
    });
  })
  .on("error", console.error)
  .end();

function generateRpcTs(meta) {
  const stream = fs.createWriteStream(outputFilePath, {
    flags: "w",
  });

  stream.write("// tslint:disable\n");

  stream.write(`import { createRpcInvoker } from "cooke-rpc";\n\n`);

  // Register all types that requires a discriminator due to participating in polymorphic scenarios
  const discriminatedTypes = new Set();
  for (const type of meta.types) {
    if (type.kind === "union") {
      for (const memberType of type.types) {
        discriminatedTypes.add(memberType.name);
      }
    } else if (type.kind === "object") {
      if (type.extends && type.extends.length > 0) {
        discriminatedTypes.add(type.name);
      }
    }
  }

  var abstractUnions = new Set();
  for (const type of meta.types) {
    if (
      type.kind === "object" &&
      type.abstract &&
      (!type.properties || type.properties.length === 0)
    ) {
      abstractUnions.add(type.name);
    }
  }

  var abstractTypes = new Set();
  for (const type of meta.types) {
    if (type.kind === "object" && type.abstract) {
      abstractTypes.add(type.name);
    }
  }

  function formatTypeUsage(type) {
    if (typeof type === "string") {
      return type;
    }

    switch (type.kind) {
      case "union":
        if (type.types.length === 0) {
          return "never";
        }

        return type.types.map(formatTypeUsage).join(" | ");

      case "generic":
        switch (type.name) {
          case "array":
            return `Array<${type.typeArguments
              .map(formatTypeUsage)
              .join(",")}>`;

          case "tuple":
            return `[${type.typeArguments.map(formatTypeUsage).join(",")}]`;

          case "optional":
            return `${type.typeArguments
              .map(formatTypeUsage)
              .join(",")} | undefined`;

          case "map": {
            const keyType = formatTypeUsage(type.typeArguments[0]);
            const valueType = formatTypeUsage(type.typeArguments[1]);
            if (keyType === "string" || keyType === "number") {
              return `{[key: ${keyType}]: ${valueType}}`;
            } else {
              return `{[key in ${keyType}]?: ${valueType}}`;
            }
          }

          default:
            return `${type.name}<${type.typeArguments
              .map(formatTypeUsage)
              .join(",")}>`;
        }

      default:
        return type.name;
    }
  }

  function writeProperties(stream, properties) {
    if (!properties) {
      return;
    }

    stream.write(
      properties
        .map(
          (p) =>
            `  ${p.name}${
              (p.type.kind === "generic" && p.type.name === "optional") ||
              p.optional
                ? "?"
                : ""
            }: ${formatTypeUsage(p.type)};`
        )
        .join("\n")
    );
  }

  function writeObject(stream, type, name) {
    stream.write(`export interface ${name} `);

    var actualExtends =
      type.extends?.filter((x) => !abstractUnions.has(x)) ?? [];
    if (actualExtends.length > 0) {
      stream.write("extends ");
      stream.write(
        actualExtends
          .map((x) => (abstractTypes.has(x) ? x + "$Base" : x))
          .join(", ")
      );
      stream.write(" ");
    }

    stream.write("{\n");
    if (!type.abstract) {
      if (discriminatedTypes.has(type.name)) {
        stream.write(`  $type: "${type.name}";\n`);
      } else {
        stream.write(`  $type?: "${type.name}";\n`);
      }
    }

    writeProperties(stream, type.properties);

    stream.write("\n}");
  }

  for (const type of meta.types) {
    if (type.kind === "primitive") {
    } else if (type.kind === "union") {
      stream.write(`export type ${type.name} = `);
      stream.write(formatTypeUsage(type));
      stream.write(";\n\n");
    } else if (type.kind === "object") {
      if (type.abstract) {
        if (!abstractUnions.has(type.name)) {
          writeObject(stream, type, type.name + "$Base");
          stream.write("\n\n");
        }

        var extenders = meta.types.filter(
          (x) => x.extends && x.extends.includes(type.name)
        );
        if (extenders.length > 0) {
          stream.write(`export type ${type.name} = `);
          stream.write(extenders.map((x) => x.name).join(" | "));
          stream.write(";\n\n");
        }
      } else {
        writeObject(stream, type, type.name);
        stream.write("\n\n");
      }
    } else if (type.kind === "enum") {
      stream.write(`export enum ${type.name} {\n`);
      stream.write(
        type.members
          .map(
            (x) =>
              `  ${x.name.match(/[^\w]/) ? `"${x.name}"` : x.name} = "${
                x.name
              }"`
          )
          .join(",\n")
      );
      stream.write("\n}\n\n");
    } else {
      throw new Error(`Unknown RPC type kind: ${type.kind}`);
    }
  }

  for (const service of meta.services) {
    stream.write(`export const ${toCamelCase(service.name)} = {\n`);

    for (const proc of service.procedures) {
      const argsType = `[${proc.parameters
        .map((p) => `${p.name}: ${formatTypeUsage(p.type)}`)
        .join(", ")}]`;
      stream.write(
        `  ${toCamelCase(proc.name)}: createRpcInvoker<'${service.name}', '${
          proc.name
        }', ${argsType}, ${formatTypeUsage(proc.returnType)}>('${
          service.name
        }', '${proc.name}'),\n`
      );
    }

    stream.write(`}\n\n`);
  }

  stream.end();
}

function toCamelCase(str) {
  if (str[0] !== str[0].toLowerCase()) {
    return str[0].toLowerCase() + str.substr(1);
  }

  return str;
}
